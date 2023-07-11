using System.Collections;
using System.Globalization;
using System.Reflection;
using Google.Protobuf;
using NetGeo.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UidConverter = Dgraph4Net.Newtonsoft.Json.UidConverter;

namespace Dgraph4Net.ActiveRecords;

internal class NJClassMapping : ClassMappingImpl
{
    public override string Serialize(object obj) =>
        JsonConvert.SerializeObject(obj);

    public override object? Deserialize(string json, Type type) =>
        JsonConvert.DeserializeObject(json, type);

    public override void SetDefaults()
    {
        GeoExtensions.SetDefaults();

        var settings = JsonConvert.DefaultSettings?.Invoke() ?? new JsonSerializerSettings();

        if (settings.Converters.Any(x => x is UidConverter))
            return;

        settings.Converters = new List<JsonConverter>(settings.Converters)
        {
            new UidConverter()
        };

        settings.Culture = CultureInfo.InvariantCulture;

        JsonConvert.DefaultSettings = () => settings;
    }

    private static JObject? GetData(ByteString bytes)
    {
        // get data content from json as json
        var element = JsonConvert.DeserializeObject<JObject?>(bytes.ToStringUtf8());

        if (element is null || element.TryGetValue("data", out var data) || data is null || data.Type != JTokenType.Object)
            return element;

        return (JObject)data;
    }

    private object? DeserializeFromJsonBS(ByteString bytes, string param, Type dataType, IList? list = null, Dictionary<Uid, object> loaded = default!, Type? type = null)
    {
        loaded ??= new();

        var element = GetData(bytes);

        if (element is null || element.Property(param ?? "_") is not JProperty parameter || parameter.Type != JTokenType.Array)
        {
            if (string.IsNullOrEmpty(param))
            {
                return FromJson(bytes, type, loaded);
            }
            else
            {
                return list;
            }
        }

        return parameter.ToList().ConvertAll(e => e.ToObject(dataType));
    }

    private object? DeserializeFromJson(ByteString bytes, Type dataType, IList list, Dictionary<Uid, object> loaded)
    {
        var element = Deserialize<JArray?>(bytes.ToStringUtf8());

        if (element is null || element.Count == 0)
            return list;

        foreach (var el in element.AsJEnumerable())
        {
            if (el is not JObject child)
                continue;

            Uid uid = child.Property("uid").ToString();

            if (uid.IsEmpty)
                continue;

            if (loaded.ContainsKey(uid))
            {
                list.Add(loaded[uid]);
                continue;
            }

            var entity = (IEntity)Activator.CreateInstance(dataType);

            dataType.GetProperty(nameof(IEntity.Uid))
                .SetValue(entity, uid);
            list.Add(entity);

            foreach (var predicate in ClassMap.Predicates.Where(x => x.Key.DeclaringType == dataType))
            {
                if (child.Property(predicate.Value.PredicateName) is not null and { } value)
                    predicate.Value.SetValue(value.ToObject(predicate.Key.PropertyType), entity);
            }
        }

        return list;
    }

    private object? DeserializeFromJson(ByteString bytes, Type dataType, Dictionary<Uid, object> loaded)
    {
        var element = JsonConvert.DeserializeObject<JObject>(bytes.ToStringUtf8());

        Uid uid = element.Property("uid").Value.ToString();

        if (uid.IsEmpty)
            return null;

        if (loaded.ContainsKey(uid))
        {
            return loaded[uid];
        }

        var entity = (IEntity)Activator.CreateInstance(dataType);

        dataType.GetProperty(nameof(IEntity.Uid))
            .SetValue(entity, uid);

        foreach (var predicate in ClassMap.Predicates.Where(x => x.Key.DeclaringType == dataType))
        {
            if (element.Property(predicate.Value.PredicateName) is not null and JProperty value)
            {
                if (predicate.Key.PropertyType == typeof(Uid))
                {
                    Uid id = value.Value.ToString();
                    predicate.Value.SetValue(id, entity);
                }
                else
                {
                    var js = JsonConvert.SerializeObject(value.Value);
                    if (value.Value.Type == JTokenType.Object && predicate.Key.PropertyType.IsAssignableTo(typeof(IEntity)))
                    {
                        predicate.Value.SetValue(FromJson(ByteString.CopyFromUtf8(js), predicate.Key.PropertyType, loaded), entity);
                    }
                    else
                    {
                        predicate.Value.SetValue(JsonConvert.DeserializeObject(js, predicate.Key.PropertyType), entity);
                    }
                }
            }
        }

        return entity;
    }

    private class JsonClassMap<T> : ClassMap<T> where T : IEntity
    {
        protected override void Map()
        {
            SetType(typeof(T).Name);

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.GetCustomAttribute<JsonPropertyAttribute>() is not null &&
                    x.Name != "Id" && x.Name != "DgraphType");

            foreach (var prop in properties)
            {
                var attr = prop.GetCustomAttribute<JsonPropertyAttribute>();

                if (prop.PropertyType.IsEnum)
                {
                    if (prop.PropertyType.GetCustomAttribute<FlagsAttribute>(true) is not null)
                    {
                        ListString(prop, attr.PropertyName);
                    }
                    else
                    {
                        String(prop, attr.PropertyName, false, false, false, StringToken.Exact, null);
                    }
                }
                else
                {
                    if (TryGetType(prop.PropertyType, out var dataType))
                    {
                        switch (dataType)
                        {
                            case "uid":
                                HasOne(prop, attr.PropertyName.Replace("~", ""), attr.PropertyName.StartsWith("~"), false);
                                break;

                            case "string":
                                String(prop, attr.PropertyName, false, false, false, StringToken.Term, null);
                                break;

                            case "int":
                                Integer(prop, attr.PropertyName, true);
                                break;

                            case "float":
                                Float(prop, attr.PropertyName, true);
                                break;

                            case "datetime":
                                DateTime(prop, attr.PropertyName, Core.DateTimeToken.Hour, false);
                                break;

                            case "geo":
                                Geo(prop, attr.PropertyName, true, false);
                                break;
                        }
                    }
                    else if (dataType.StartsWith("["))
                    {
                        switch (dataType)
                        {
                            case "[uid]":
                                HasMany(prop, attr.PropertyName);
                                break;

                            case "[string]":
                            case "[int]":
                            case "[float]":
                            case "[datetime]":
                            case "[geo]":
                                var dt = dataType[1..^1];
                                List(prop, dt, attr.PropertyName);
                                break;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Inverse of <see cref="ToJson{T}(T, Dictionary{Uid, object}, bool, bool)"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bytes"></param>
    /// <param name="loaded"></param>
    /// <returns>DB Result</returns>
    public override object? FromJsonBS(ByteString bytes, Type type, string param)
    {
        if (bytes.IsEmpty)
            return default;

        if (type.IsAssignableTo(typeof(Dictionary<string, object>)))
            return Deserialize<Dictionary<string, object>>(bytes.ToStringUtf8());

        Type dataType;

        var loaded = new Dictionary<Uid, object>();

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

            return DeserializeFromJsonBS(bytes, param, dataType, list, loaded, type);
        }
        else
        {
            return Deserialize(bytes.ToStringUtf8(), type);
        }
    }

    public override object? FromJson(ByteString bytes, Type type, Dictionary<Uid, object> loaded)
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

    public override bool TryMapJson(Type type, out IClassMap? classMap)
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

    public override void Map()
    {
        SetDefaults();
        base.Map();
    }

    public override void Map(params Assembly[] assemblies)
    {
        SetDefaults();
        base.Map(assemblies);
    }
}
