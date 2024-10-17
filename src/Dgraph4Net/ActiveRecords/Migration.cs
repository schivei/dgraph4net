using System.Globalization;
using System.Reflection;
using System.Text;
using Api;
using Google.Protobuf;

namespace Dgraph4Net.ActiveRecords;

public abstract class Migration : AEntity<Migration>, IDgnMigration
{
    private IDgraph4NetClient _client;

    internal ISet<IClassMap> Types { get; } = new HashSet<IClassMap>();
    internal ISet<string> DropTypes { get; } = new HashSet<string>();
    internal ISet<string> DropPredicates { get; } = new HashSet<string>();
    public DateTimeOffset AppliedAt { get; }
    public DateTimeOffset GeneratedAt { get; }
    public string Name { get; }

    internal void SetClient(IDgraph4NetClient client) => _client = client;

    /// <summary>
    /// Get *.cs.schema embedded resource from migration assembly
    /// </summary>
    /// <returns>The schema content</returns>
    internal async Task<string> GetSchema()
    {
        var t = GetType();
        var assembly = t.Assembly;

        // get all assembly and referenced assembly resources in ./cs folder at same location of assembly, need to load that first
        var assemblyPath = assembly.Location;
        var assemblyDir = Path.GetDirectoryName(assemblyPath) ?? throw new Exception("Assembly path not found.");
        var assemblyCsDir = Path.Combine(assemblyDir, "cs");
        if (Directory.Exists(assemblyCsDir) &&
            Directory.GetFiles(assemblyCsDir, $"{assembly.GetName().Name}.resources.dll")
            .FirstOrDefault() is { } assemblyCsFile and not null)
        {
            assembly = Assembly.LoadFile(assemblyCsFile);
        }

        var schema = $"{t.FullName}.schema";

        await using var stream = assembly.GetManifestResourceStream(schema) ?? throw new Exception("Schema not found.");

        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync();
    }

    protected abstract void Up();

    protected abstract void Down();

    internal void Load() => Up();

    internal Task MigrateUp() => Migrate();

    internal Task MigrateDown() => Migrate(true);

    protected void DropPredicate(string predicateName) =>
        DropPredicates.Add(predicateName);

    protected void SetType<T>() where T : IEntity => Types.Add(InternalClassMapping.ClassMappings[typeof(T)]);

    protected void DropType(string name) => DropTypes.Add(name);

    protected Migration()
    {
        var t = GetType();
        var name = t.Name;

        GeneratedAt = DateTimeOffset.TryParseExact(name.Split("_", StringSplitOptions.RemoveEmptyEntries).Last(), "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result) ?
            result : DateTimeOffset.MinValue;

        if (GeneratedAt == DateTimeOffset.MinValue)
            throw new Exception($"Invalid migration name '{name}'.");

        Name = name.Split("_", StringSplitOptions.RemoveEmptyEntries).First();
    }

    private async Task Migrate(bool down = false, CancellationToken cancellationToken = default)
    {
        await InternalClassMapping.EnsureAsync(_client);

        DgnMigration dgnm = new(this);

        var dgnType = InternalClassMapping.ClassMappings[typeof(DgnMigration)].DgraphType ?? "dgn.migration";

        {
            await using var txn = _client.NewTransaction(false, false, cancellationToken);

            var migs = await txn.QueryWithVars<DgnMigration>("dgn", @$"query Q($name: string) {{
  dgn(func: type({dgnType}), first: 1) @filter(eq(dgn.name, $name)) {{
    uid
    dgraph.type
    dgn.name
    dgn.generated_at
    dgn.applied_at
  }}
}}", new Dictionary<string, string> { { "$name", dgnm.Name } });

            if (migs.Count == 0)
            {
                Up();
            }
            else if (down)
            {
                dgnm = migs[0];
                Down();
            }
            else
            {
                return;
            }
        }

        if (DropTypes.Any())
        {
            foreach (var dropType in DropTypes)
            {
                await _client.Alter(new Operation
                {
                    DropOp = Operation.Types.DropOp.Type,
                    DropValue = dropType,
                    RunInBackground = true
                });
            }
        }

        if (DropPredicates.Any())
        {
            foreach (var pre in DropPredicates)
            {
                await using var txn = _client.NewTransaction(false, false, cancellationToken);

                try
                {
                    await txn.MutateWithQuery(new Mutation
                    {
                        DelNquads = new NQuad { Subject = "uid(d)", Predicate = pre, ObjectValue = new Value { DefaultVal = "_STAR_ALL" } }.ToByteString(),
                        CommitNow = true
                    }, $"d as var(func: has({pre}))");
                }
                catch
                {
                    // ignored
                }

                await _client.Alter(new Operation
                {
                    DropOp = Operation.Types.DropOp.Attr,
                    DropValue = pre,
                    RunInBackground = true
                });
            }
        }

        if (Types.Any())
        {
            foreach (var map in Types)
            {
                var pre = ClassMap.Predicates.Values.Where(y => y.ClassMap == map).ToArray();

                var predicates = pre
                      .Where(x => x is not UidPredicate and not TypePredicate)
                      .GroupBy(x => x.PredicateName)
                      .Where(x => !string.IsNullOrEmpty(x.Key))
                      .Select(x => (x.Key, p: x.Aggregate((p1, p2) => p1.Merge(p2))))
                      .Select(x => (x.Key, p: x.p.ToSchemaPredicate()))
                      .Where(x => !string.IsNullOrEmpty(x.p))
                      .OrderBy(x => x.Key)
                      .Aggregate(new StringBuilder(), (sb, x) => sb.AppendLine(x.p), sb => sb.ToString());

                var type = new StringBuilder()
                      .Append("type ")
                      .Append(map.DgraphType)
                      .AppendLine(" {")
                      .AppendJoin('\n', pre
                          .Where(x => x is not UidPredicate and not TypePredicate)
                          .GroupBy(x => x.PredicateName)
                          .Where(x => !string.IsNullOrEmpty(x.Key))
                          .AsParallel()
                          .Select(x => (x.Key, p: x.Aggregate((p1, p2) => p1.Merge(p2))))
                          .Select(x => (x.Key, p: x.p.ToTypePredicate()))
                          .Where(x => !string.IsNullOrEmpty(x.p))
                          .ToArray()
                          .OrderBy(x => x.Key)
                          .Select(x => $"  <{x.p}>"))
                      .AppendLine()
                      .AppendLine("}")
                      .ToString();

                try
                {
                    await _client.Alter(new Operation
                    {
                        Schema = $"{predicates}\n{type}\n",
                        RunInBackground = true
                    });
                }
                catch
                {
                    // ignored
                }
            }
        }

        dgnm.Uid.Resolve();

        if (down)
        {
            // remove migration data
            await using var txn = _client.NewTransaction(false, false, cancellationToken);

            await txn.Mutate(new Mutation
            {
                CommitNow = true,
                DelNquads = new NQuad { Subject = $"<{dgnm.Uid}>" }.ToByteString()
            });
        }
        else if (!dgnm.Uid.IsConcrete)
        {
            dgnm.AppliedAt = DateTimeOffset.UtcNow;

            // add migration data
            await using var txn = _client.NewTransaction(false, false, cancellationToken);

            var mutation = new Mutation
            {
                CommitNow = true,
                SetJson = ClassMapping.ToJson(dgnm)
            };

            try
            {
                await txn.Mutate(mutation);
            }
            catch (Exception ex)
            {
                var mut = mutation.SetJson.ToStringUtf8();
                throw new Exception($"Error on mutate with: {mut}", ex);
            }
        }
    }
}
