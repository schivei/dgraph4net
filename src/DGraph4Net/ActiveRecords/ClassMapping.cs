using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dgraph4Net.Core.GeoLocation;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static Pb.Metadata.Types;

#nullable enable

namespace Dgraph4Net.ActiveRecords;

public static class ClassMapping
{
    internal static ConcurrentDictionary<Type, IClassMap> ClassMappings { get; }

    internal static ConcurrentBag<Migration> Migrations { get; private set; }

    static ClassMapping()
    {
        ClassMappings = new ConcurrentDictionary<Type, IClassMap>();
    }

    public static IServiceCollection AddDgraph(this IServiceCollection services) =>
        services.AddDgraph("DefaultConnection");

    public static IServiceCollection AddDgraph(this IServiceCollection services, string connectionName) =>
        services.AddDgraph(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString(connectionName));

    public static IServiceCollection AddDgraph(this IServiceCollection services, Func<IServiceProvider, string> getConnectionString)
    {
        services.AddTransient<ChannelBase>(sp =>
        {
            var connectionString = getConnectionString(sp);

            var server = Array.Find(connectionString
                .Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                s => s.StartsWith("server=", StringComparison.InvariantCultureIgnoreCase))?
                .Split('=')[1] ?? "localhost:9080";

            var useTls = Array.Find(connectionString
                               .Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                                              s => s.StartsWith("use tls=", StringComparison.InvariantCultureIgnoreCase))?
                .Split('=')[1].ToLowerInvariant() ?? "true";

            return useTls == "false" ?
                new Channel(server, ChannelCredentials.Insecure) :
                new Channel(server, ChannelCredentials.SecureSsl);
        });

        services.AddTransient(sp =>
        {
            var connectionString = getConnectionString(sp);

            var userId = Array.Find(connectionString
                               .Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                                              s => s.StartsWith("user id=", StringComparison.InvariantCultureIgnoreCase))?
                .Split('=')[1];

            var password = Array.Find(connectionString
                                              .Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                                                                                           s => s.StartsWith("password=", StringComparison.InvariantCultureIgnoreCase))?
                .Split('=')[1];

            var client = new Dgraph4NetClient(sp.GetRequiredService<ChannelBase>());

            if (userId is not null)
                client.Login(userId, password);

            return client;
        });

        Map();

        return services;
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
            .Select(ClassMap.CreateInstance)
            .ToList();

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
        .Select(x => (Migration)Activator.CreateInstance(x))
        .ToList();

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

    public static ByteString ToJson<T>(this T entity, bool deep = false, bool doNotPropagateNulls = false) where T : IEntity =>
        ByteString.CopyFromUtf8(ToJson(entity, new(), deep, doNotPropagateNulls));

    private static string ToJson<T>(this T entity, HashSet<IEntity> mapped, bool deep, bool doNotPropagateNulls = false) where T : IEntity
    {
        var type = typeof(T);

        if (!ClassMappings.TryGetValue(type, out var classMap))
            throw new InvalidOperationException($"ClassMap for {type.Name} not found");

        var json = new StringBuilder();

        json.Append("{ ");

        entity.Id.Resolve();

        var dtype = classMap.DgraphType;

        json.Append("\"uid\": \"").Append(entity.Id.ToString()).Append("\",");

        if (!mapped.Contains(entity))
        {
            mapped.Add(entity);

            json.Append("\"dgraph.type\": \"").Append(dtype).Append("\",");

            var predicates = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.CanRead && ClassMap.Predicates.ContainsKey(x))
                .Select(x => (Property: x, Predicate: ClassMap.Predicates[x], Value: x.GetValue(entity)))
                .Where(x => x.Predicate is not UidPredicate and not TypePredicate)
                .ToImmutableArray();

            if (!predicates.Any())
                throw new InvalidOperationException($"No predicates found for {type.Name}");

            foreach (var (property, predicate, value) in predicates)
            {
                if (predicate is BooleanPredicate boolean)
                {
                    if (value is bool bl)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": ")
                            .Append(bl.ToString().ToLower()).Append(',');
                    }
                    else if (!doNotPropagateNulls)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": null,");
                    }
                }
                else if (predicate is DateTimePredicate date)
                {
                    if (value is DateTime dt)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": \"")
                            .Append(dt.ToString("O")).Append("\",");
                    }
                    else if (value is DateTimeOffset dto)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": \"")
                            .Append(dto.ToString("O")).Append("\",");
                    }
                    else if (value is DateOnly d)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": \"")
                            .Append(d.ToString("yyyy-MM-dd")).Append("\",");
                    }
                    else if (!doNotPropagateNulls)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": null,");
                    }
                }
                else if (predicate is IEdgePredicate edge)
                {
                    var edgeValue = value as IEntity;

                    if (edgeValue is not null)
                    {
                        edgeValue.Id.Resolve();

                        if (deep && !edgeValue.Id.IsConcrete)
                        {
                            json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": ");

                            json.Append(edgeValue.ToJson(mapped, deep, doNotPropagateNulls)).Append(',');
                        }
                        else if (edgeValue.Id.IsConcrete)
                        {
                            json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": { \"uid\": \"")
                                .Append(edgeValue.Id.ToString()).Append("\" },");
                        }
                        else
                        {
                            json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": null,");
                        }
                    }
                    else
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": null,");
                    }
                }
                else if (predicate is FloatPredicate flt)
                {
                    if (value is Enum en)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": ")
                            .Append(Convert.ToInt64(en)).Append(',');
                    }
                    else if (value is float f)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": ")
                            .Append(f.ToString("R", CultureInfo.InvariantCulture)).Append(',');
                    }
                    else if (value is double d)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": ")
                            .Append(d.ToString("R", CultureInfo.InvariantCulture)).Append(',');
                    }
                    else if (value is decimal dec)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": ")
                            .Append(dec.ToString("R", CultureInfo.InvariantCulture)).Append(',');
                    }
                    else if (value is TimeOnly dto)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": ")
                            .Append(dto.ToTimeSpan().Ticks.ToString(CultureInfo.InvariantCulture)).Append(',');
                    }
                    else if (value is TimeSpan ts)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": ")
                                                .Append(ts.Ticks.ToString(CultureInfo.InvariantCulture)).Append(',');
                    }
                    else if (!doNotPropagateNulls)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": null,");
                    }
                }
                else if (predicate is GeoPredicate geo)
                {
                    if (value is IGeoObject obj)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": ")
                            .Append(obj.ToGeoJson()).Append(',');
                    }
                    else if (!doNotPropagateNulls)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": null,");
                    }
                }
                else if (predicate is IntegerPredicate itg)
                {
                    if (value is not null)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": ")
                            .Append(Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture))
                            .Append(',');
                    }
                    else if (!doNotPropagateNulls)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": null,");
                    }
                }
                else if (predicate is ListPredicate list)
                {
                    if (list.ListType == "uid" && property.PropertyType.IsAssignableTo(typeof(IEnumerable<IEntity>)))
                    {
                        var listValue = (IEnumerable<IEntity>)value;

                        if (listValue.Any())
                        {
                            var listData = listValue.Select(item =>
                            {
                                item.Id.Resolve();

                                if (deep && !item.Id.IsConcrete)
                                {
                                    return item.ToJson(mapped, deep, doNotPropagateNulls);
                                }
                                else if (item.Id.IsConcrete)
                                {
                                    return $"{{ \"uid\": \"{item.Id}\" }}";
                                }

                                return string.Empty;
                            }).Where(x => !string.IsNullOrEmpty(x));

                            if (listData.Any())
                            {
                                json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": ")
                                    .AppendJoin(',', listData).Append(',');
                            }
                            else if (!doNotPropagateNulls)
                            {
                                json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": [],");
                            }
                        }
                    }
                    else if (property.PropertyType.IsAssignableTo(typeof(IEnumerable)))
                    {
                        var listValue = ((IEnumerable)value).Cast<object>();

                        if (listValue.Any())
                        {
                            json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": ");

                            json.Append(JsonSerializer.Serialize(listValue)).Append(',');
                        }
                        else if (!doNotPropagateNulls)
                        {
                            json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": [],");
                        }
                    }
                    else if (property.PropertyType.IsEnum)
                    {
                        var en = (Enum)value;

                        var values = en.GetFlaggedValues();

                        if (list.ListType == "string")
                        {
                            var strValues = values.Select(x => x.ToString());

                            if (strValues.Any())
                            {
                                json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": ")
                                    .AppendJoin(',', strValues);
                            }
                            else if (!doNotPropagateNulls)
                            {
                                json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": []");
                            }
                        }
                        else
                        {
                            var intValues = values.Select(Convert.ToInt64);

                            if (intValues.Any())
                            {
                                json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": ")
                                    .AppendJoin(',', intValues).Append(',');
                            }
                            else if (!doNotPropagateNulls)
                            {
                                json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": [],");
                            }
                        }
                    }
                }
                else
                {
                    var strValue = value?.ToString();
                    if (strValue is not null)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": ")
                            .Append(JsonSerializer.Serialize(strValue)).Append(','); // to escape quotes
                    }
                    else if (!doNotPropagateNulls)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": null,");
                    }
                }
            }
        }

        var jsonStr = json.ToString();

        if (jsonStr.EndsWith(","))
            jsonStr = jsonStr[..^1];

        jsonStr += " }";

        return jsonStr;
    }

    public static T? FromJson<T>(this ByteString bytes, string param) =>
        bytes.FromJson<T>(param, new());

    /// <summary>
    /// Inverse of <see cref="ToJson{T}(T, Dictionary{Uid, object}, bool, bool)"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bytes"></param>
    /// <param name="loaded"></param>
    /// <returns>DB Result</returns>
    private static T? FromJson<T>(this ByteString bytes, string param, Dictionary<Uid, object> loaded)
    {
        if (bytes.IsEmpty)
            return default;

        var type = typeof(T);

        if (type.IsAssignableTo(typeof(Dictionary<string, object>)))
            return (T)(object)JsonSerializer.Deserialize<Dictionary<string, object>>(bytes.Span);

        Type dataType;

        if (type.IsAssignableTo(typeof(IEnumerable)))
        {
            dataType = type.GetGenericArguments()[0];
        }
        else
        {
            dataType = type;
        }

        if (dataType.IsAssignableTo(typeof(IEntity)))
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(dataType));

            var element = JsonSerializer.Deserialize<JsonElement?>(bytes.Span);

            if (!element.HasValue || !element.Value.TryGetProperty(param, out var children) || children.ValueKind != JsonValueKind.Array)
                return (T)list;

            foreach (var child in children.EnumerateArray())
            {
                Uid uid = child.GetProperty("uid").GetString();

                if (uid.IsEmpty)
                    continue;

                if (loaded.ContainsKey(uid))
                {
                    list.Add(loaded[uid]);
                    continue;
                }

                var entity = (IEntity)Activator.CreateInstance(dataType);

                dataType.GetProperty(nameof(IEntity.Id))
                    .SetValue(entity, uid);

                list.Add(entity);

                var predicates = ClassMap.Predicates.Where(x => x.Key.DeclaringType == dataType);

                foreach (var predicate in predicates)
                {
                    if (child.TryGetProperty(predicate.Value.PredicateName, out var value))
                        predicate.Value.SetValue(value, entity);
                }
            }

            return (T)list;
        }
        else
        {
            return JsonSerializer.Deserialize<T>(bytes.Span);
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
}
