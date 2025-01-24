using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

using Google.Protobuf;

namespace Dgraph4Net.ActiveRecords;

internal static class InternalClassMapping
{
    internal static List<IPredicate> GetPredicates(Type type) =>
        ClassMap.GetPredicates(type).ToList();

    internal static IPredicate GetPredicate(PropertyInfo prop) =>
        ClassMap.GetPredicate(prop);

    internal static IPredicate GetPredicate<T>(string predicateName) where T : AEntity<T> =>
        ClassMap.GetPredicate<T>(predicateName);

    internal static IPredicate GetPredicate(Type objectType, string predicateName) =>
        ClassMap.GetPredicate(objectType, predicateName);

    internal static ConcurrentBag<Migration> Migrations { get; set; }

    internal static void SetDefaults(Assembly[] assemblies)
    {
        var classMapping = assemblies.SelectMany(x =>
        {
            try
            {
                return x.GetTypes();
            }
            catch
            {
                return [];
            }
        }).FirstOrDefault(x => x.Name == nameof(ClassMappingImpl) && x.IsClass && x is { IsAbstract: false, Namespace: "Dgraph4Net.ActiveRecords" }
        );

        classMapping.GetMethod("SetDefaults", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
    }

    public static void Map(params Assembly[] assemblies)
    {
        var mappings = assemblies.SelectMany(x =>
        {
            try
            {
                return x.GetTypes();
            }
            catch
            {
                return [];
            }
        })
            .Where(x => x.IsAssignableTo(typeof(IClassMap)) && x is { IsClass: true, IsAbstract: false })
            .Distinct()
            .Select(x =>
            {
                try
                {
                    return ClassMap.CreateInstance(x);
                }
                catch
                {
                    return null;
                }
            }).Where(x => x is not null).ToList();

        mappings.ForEach(x =>
        {
            x.Start();
            x.Finish();

            Dgraph4Net.InternalClassMapping.ClassMappings.TryAdd(x.Type, x);
        });

        var migrations = assemblies.SelectMany(x =>
        {
            try
            {
                return x.GetTypes();
            }
            catch
            {
                return [];
            }
        }).Where(x => x.IsAssignableTo(typeof(Migration)) && x is { IsClass: true, IsAbstract: false })
        .Select(x =>
        {
            try
            {
                return (Migration)Activator.CreateInstance(x);
            }
            catch
            {
                return null;
            }
        }).Where(x => x is not null).ToList();

        Migrations = new(migrations);
    }

    internal static string CreateScript()
    {
        var predicates = ClassMap.Predicates.Values
        .Where(x => x is not UidPredicate and not TypePredicate)
            .GroupBy(x => x.PredicateName)
            .Where(x => !string.IsNullOrEmpty(x.Key))
            .Select(x => (x.Key, p: x.Aggregate((p1, p2) => p1.Merge(p2))))
            .Select(x => (x.Key, p: x.p.ToSchemaPredicate()))
            .Where(x => !string.IsNullOrEmpty(x.p))
            .OrderBy(x => x.Key)
            .Aggregate(new StringBuilder(), (sb, x) => sb.AppendLine(x.p), sb => sb.ToString());

        var types = Dgraph4Net.InternalClassMapping.ClassMappings.Values
            .Select(x => (classMap: x, predicates: ClassMap.Predicates.Values.Where(y => y.ClassMap == x).ToArray()))
            .DistinctBy(x => x.classMap.DgraphType)
            .OrderBy(x => x.classMap.DgraphType)
            .Aggregate(new StringBuilder(), (sb, type) =>
                sb.Append("type ")
                  .Append(type.classMap.DgraphType)
                  .AppendLine(" {")
                  .AppendJoin('\n', type.predicates
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
                  .AppendLine()
            , sb => sb.ToString().Replace("\r\n", "\n").Replace("\r", "\n").Trim('\n') + "\n");

        return new StringBuilder().AppendLine(predicates).AppendLine(types).ToString()
            .Replace("\r\n", "\n").Replace("\r", "\n");
    }

    internal static async Task EnsureAsync(IDgraph4NetClient client)
    {
        const string script = """

                              dgn.applied_at: dateTime @index(day) .
                              dgn.generated_at: dateTime @index(hour) .
                              dgn.name: string @index(exact) .

                              type dgn.migra()t                         dgn.applied_at
                                dgn.generated_at
                                dgn.name
                              }
                              """;

        try
        {
            await client.Alter(new() { Schema = script, RunInBackground = true });
        }
        catch
        {
            // ignored
        }
    }

    public static void Map() =>
        Map(AppDomain.CurrentDomain.GetAssemblies());


    internal static ConcurrentDictionary<Type, IClassMap> ClassMappings => Dgraph4Net.InternalClassMapping.ClassMappings;

    internal static object? ToJsonByteStringFuncInstance
    {
        get => Dgraph4Net.InternalClassMapping.ToJsonByteStringFuncInstance;
        set => Dgraph4Net.InternalClassMapping.ToJsonByteStringFuncInstance = value;
    }

    internal static MethodInfo? ToJsonByteStringFunc
    {
        get => Dgraph4Net.InternalClassMapping.ToJsonByteStringFunc;
        set => Dgraph4Net.InternalClassMapping.ToJsonByteStringFunc = value;
    }

    internal static object? FromJsonByteStringFuncInstance
    {
        get => Dgraph4Net.InternalClassMapping.FromJsonByteStringFuncInstance;
        set => Dgraph4Net.InternalClassMapping.FromJsonByteStringFuncInstance = value;
    }

    internal static MethodInfo? FromJsonByteStringFunc
    {
        get => Dgraph4Net.InternalClassMapping.FromJsonByteStringFunc;
        set => Dgraph4Net.InternalClassMapping.FromJsonByteStringFunc = value;
    }

    public static string GetDgraphType(Type type) =>
        ClassMappings.TryGetValue(type, out var classMap) ? classMap.DgraphType : type.Name;

    public static string GetDgraphType<T>() =>
        GetDgraphType(typeof(T));

    public static List<Enum> GetFlaggedValues(this Enum value) =>
        Dgraph4Net.InternalClassMapping.GetFlaggedValues(value);

    public static ByteString ToJson<T>(this T entity) where T : IEntity =>
        ToJson([entity]);

    public static ByteString ToJson<T>(this IEnumerable<T> entities) where T : IEntity =>
        ToJsonByteStringFunc.MakeGenericMethod(typeof(T)).Invoke(ToJsonByteStringFuncInstance, [entities]) as ByteString;

    public static object? FromJson(this ByteString bytes, Type type) =>
        FromJsonByteStringFunc.Invoke(FromJsonByteStringFunc, [bytes, type]);
}
