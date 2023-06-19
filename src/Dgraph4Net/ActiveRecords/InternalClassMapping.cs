using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Google.Protobuf;

namespace Dgraph4Net.ActiveRecords;

internal static class InternalClassMapping
{
    internal static List<IPredicate> GetPredicates(Type type) =>
      ClassMap.Predicates.Where(x => x.Key.DeclaringType == type).Select(x => x.Value).ToList();

    internal static IPredicate GetPredicate(PropertyInfo prop) =>
        ClassMap.Predicates.First(x => x.Key == prop).Value;

    internal static ConcurrentDictionary<Type, IClassMap> ClassMappings { get; }

    internal static ConcurrentBag<Migration> Migrations { get; set; }

    internal static MethodInfo ToJsonByteStringFunc { get; set; }

    internal static MethodInfo FromJsonByteStringFunc { get; set; }

    static InternalClassMapping()
    {
        ClassMappings = new ConcurrentDictionary<Type, IClassMap>();
    }

    public static void Map() =>
        Map(AppDomain.CurrentDomain.GetAssemblies());

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
                return Array.Empty<Type>();
            }
        })
            .Where(x => x.IsAssignableTo(typeof(IClassMap)) && x.IsClass && !x.IsAbstract)
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

            ClassMappings.TryAdd(x.Type, x);
        });

        var migrations = assemblies.SelectMany(x =>
        {
            try
            {
                return x.GetTypes();
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }).Where(x => x.IsAssignableTo(typeof(Migration)) && x.IsClass && !x.IsAbstract)
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

        var types = ClassMappings.Values
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

    public static string GetDgraphType(Type type)
    {
        if (ClassMappings.TryGetValue(type, out var classMap))
            return classMap.DgraphType;

        return type.Name;
    }

    public static string GetDgraphType<T>() =>
        GetDgraphType(typeof(T));

    internal static async Task EnsureAsync(IDgraph4NetClient client)
    {
        const string script = @"
dgn.applied_at: dateTime @index(day) .
dgn.generated_at: dateTime @index(hour) .
dgn.name: string @index(exact) .

type dgn.migration {
  dgn.applied_at
  dgn.generated_at
  dgn.name
}";

        try
        {
            await client.Alter(new Api.Operation { Schema = script, RunInBackground = true });
        }
        catch
        {
            // ignored
        }
    }

    public static List<Enum> GetFlaggedValues(this Enum value)
    {
        Type enumType = value.GetType();
        object[] attributes = enumType.GetCustomAttributes(true);

        bool hasFlags = enumType.GetCustomAttributes(true).Any(attr => attr is FlagsAttribute);

        List<Enum> values = new();
        if (hasFlags)
        {
            foreach (Enum currValue in Enum.GetValues(enumType))
            {
                if (value.HasFlag(currValue))
                {
                    values.Add(currValue);
                }
            }
        }
        else
        {
            values.Add(value);
        }

        return values;
    }

    public static ByteString ToJson<T>(this T entity, bool deep = false, bool doNotPropagateNulls = false) where T : IEntity =>
        ToJsonByteStringFunc.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { entity, deep, doNotPropagateNulls }) as ByteString;

    public static object? FromJson(this ByteString bytes, Type type) =>
        FromJsonByteStringFunc.Invoke(null, new object[] { bytes, type });

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
                return Array.Empty<Type>();
            }
        }).FirstOrDefault(x => x.Name == "ClassMapping" && x.IsClass && !x.IsAbstract && (
            x.Namespace == "Dgraph4Net.ActiveRecords"
        ));

        classMapping.GetMethod("SetDefaults", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
    }
}
