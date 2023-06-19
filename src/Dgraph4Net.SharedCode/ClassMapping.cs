using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetGeo.Json;

namespace Dgraph4Net.ActiveRecords;

public static partial class ClassMapping
{
    internal static ConcurrentDictionary<Type, IClassMap> ClassMappings => InternalClassMapping.ClassMappings;

    internal static ConcurrentBag<Migration> Migrations
    {
        get => InternalClassMapping.Migrations;
        private set => InternalClassMapping.Migrations = value;
    }

    public static string ToJsonString<T>(this T entity, bool deep = false, bool doNotPropagateNulls = false) where T : IEntity =>
        ToJson(entity, deep, doNotPropagateNulls).ToStringUtf8();

    public static ByteString ToJson<T>(this T entity, bool deep = false, bool doNotPropagateNulls = false) where T : IEntity =>
        ByteString.CopyFromUtf8(ToJson(entity, new(), deep, doNotPropagateNulls).Trim());

    private static string ToJson<T>(this T entity, HashSet<IEntity> mapped, bool deep, bool doNotPropagateNulls = false) where T : IEntity
    {
        var type = entity.GetType();

        if (!TryMapJson(type, out var classMap))
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
                if (value is null)
                {
                    if (doNotPropagateNulls)
                        continue;

                    json.Append('\"').Append(predicate.PredicateName).Append("\": null,");
                    continue;
                }

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
                    if (value is GeoObject obj)
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

                            json.Append(Serialize(listValue)).Append(',');
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
                    var strValue =
                        value is byte[] bytes ?
                        Convert.ToBase64String(bytes) :
                        value?.ToString();
                    if (strValue is not null)
                    {
                        json.Append('\"').Append(predicate.ToTypePredicate()).Append("\": ")
                            .Append(Serialize(strValue)).Append(','); // to escape quotes
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

    public static object? FromJson(this ByteString bytes, Type type, string param) =>
        bytes.FromJsonBS(type, param);

    public static T? FromJson<T>(this ByteString bytes, string param) =>
        (T?)bytes.FromJsonBS(typeof(T), param);

    /// <summary>
    /// Inverse of <see cref="ToJson{T}(T, Dictionary{Uid, object}, bool, bool)"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bytes"></param>
    /// <param name="loaded"></param>
    /// <returns>DB Result</returns>
    private static object? FromJsonBS(this ByteString bytes, Type type, string param)
    {
        if (bytes.IsEmpty)
            return default;

        if (type.IsAssignableTo(typeof(Dictionary<string, object>)))
            return Deserialize<Dictionary<string, object>>(bytes.ToStringUtf8());

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

            return DeserializeFromJsonBS(bytes, param, dataType, list, type: type);
        }
        else
        {
            return Deserialize(bytes.ToStringUtf8(), type);
        }
    }

    public static object? FromJson(this string str, Type type) =>
        ByteString.CopyFromUtf8(str).FromJson(type, string.Empty);

    public static T? FromJson<T>(this string str) =>
        ByteString.CopyFromUtf8(str).FromJson<T>();

    public static object? FromJson(this string str, Type type, string param) =>
        ByteString.CopyFromUtf8(str).FromJson(type, param);

    public static T? FromJson<T>(this string str, string param) =>
        ByteString.CopyFromUtf8(str).FromJson<T>(param);

    public static object? FromJson(this ByteString bytes, Type type) =>
        bytes.FromJson(type, new Dictionary<Uid, object>());

    public static T? FromJson<T>(this ByteString bytes) =>
        (T?)bytes.FromJson(typeof(T), new Dictionary<Uid, object>());

    private static object? FromJson(this ByteString bytes, Type type, Dictionary<Uid, object> loaded)
    {
        if (bytes.IsEmpty)
            return default;

        if (type.IsAssignableTo(typeof(Dictionary<string, object>)))
            return Deserialize<Dictionary<string, object>>(bytes.ToStringUtf8());

        Type dataType;

        if (type.IsAssignableTo(typeof(IEnumerable)))
        {
            dataType = type.GetGenericArguments()[0];
        }
        else
        {
            dataType = type;
        }

        if (dataType.IsAssignableTo(typeof(IEntity)) && type != dataType)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(dataType));

            return DeserializeFromJson(bytes, dataType, list, loaded);
        }
        else if (dataType.IsAssignableTo(typeof(IEntity)))
        {
            return DeserializeFromJson(bytes, dataType, loaded);
        }

        return null;
    }

    private static bool TryMapJson(Type type, out IClassMap? classMap)
    {
        if (ClassMappings.TryGetValue(type, out classMap))
            return true;

        var mapType = typeof(JsonClassMap<>).MakeGenericType(type);

        if (Activator.CreateInstance(mapType) is IClassMap map)
        {
            classMap = map;
            ClassMappings.TryAdd(type, map);

            map.Start();
            map.Finish();

            return true;
        }

        return false;
    }

    private partial class JsonClassMap<T> : ClassMap<T> where T : IEntity { }

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

    public static void Map()
    {
        SetDefaults();
        InternalClassMapping.Map();
    }

    public static void Map(params Assembly[] assemblies)
    {
        SetDefaults();
        InternalClassMapping.Map(assemblies);
    }

    internal static Task EnsureAsync(Dgraph4NetClient client) =>
        InternalClassMapping.EnsureAsync(client);

    public static string GetDgraphType(Type type) =>
        InternalClassMapping.GetDgraphType(type);

    internal static string CreateScript() =>
        InternalClassMapping.CreateScript();

    public static List<IPredicate> GetPredicates(Type type) =>
        InternalClassMapping.GetPredicates(type);

    public static List<IPredicate> GetPredicates<T>() =>
        InternalClassMapping.GetPredicates(typeof(T));

    public static IPredicate GetPredicate(PropertyInfo prop) =>
        InternalClassMapping.GetPredicate(prop);
}
