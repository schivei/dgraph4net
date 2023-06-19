using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Protobuf;
using NetGeo.Json;

namespace Dgraph4Net.ActiveRecords;

public static partial class ClassMapping
{
    private static string Serialize(object obj) =>
        JsonSerializer.Serialize(obj);

    private static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json);

    private static object? Deserialize(string json, Type type) =>
        JsonSerializer.Deserialize(json, type);

    public static void SetDefaults()
    {
        GeoExtensions.SetDefaults();

        if (JsonSerializerOptions.Default.Converters.Any(x => x is UidConverter))
            return;

        var converters = new List<JsonConverter>(JsonSerializerOptions.Default.Converters)
            {
                new UidConverter()
            };

        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .SingleOrDefault(t => t.FullName == "System.Text.Json.JsonSerializerOptions+ConverterList");
        object[] paramValues = new object[] { JsonSerializerOptions.Default, converters };
        var converterList = type!.GetConstructors()[0].Invoke(paramValues) as IList<JsonConverter>;
        typeof(JsonSerializerOptions).GetRuntimeFields().Single(f => f.Name == "_converters")
        .SetValue(JsonSerializerOptions.Default, converterList);
    }

    private static object? DeserializeFromJsonBS(ByteString bytes, string param, Type dataType, IList? list = null, Dictionary<Uid, object> loaded = default!, Type? type = null)
    {
        loaded ??= new();

        var element = JsonSerializer.Deserialize<JsonElement?>(bytes.Span);

        if (!element.HasValue || !element.Value.TryGetProperty(param, out var children) || children.ValueKind != JsonValueKind.Array)
            return list;

        return ByteString.CopyFromUtf8(JsonSerializer.Serialize(children)).FromJson(type, loaded);
    }

    private static object? DeserializeFromJson(ByteString bytes, Type dataType, IList list, Dictionary<Uid, object> loaded)
    {
        var element = JsonSerializer.Deserialize<JsonElement?>(bytes.Span);

        if (!element.HasValue)
            return list;

        foreach (var child in element.Value.EnumerateArray())
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

            foreach (var predicate in ClassMap.Predicates.Where(x => x.Key.DeclaringType == dataType))
            {
                if (child.TryGetProperty(predicate.Value.PredicateName, out var value))
                    predicate.Value.SetValue(value.Deserialize(predicate.Key.PropertyType), entity);
            }
        }

        return list;
    }

    private static object? DeserializeFromJson(ByteString bytes, Type dataType, Dictionary<Uid, object> loaded)
    {
        var element = JsonSerializer.Deserialize<JsonElement>(bytes.Span);

        Uid uid = element.GetProperty("uid").GetString();

        if (uid.IsEmpty)
            return null;

        if (loaded.ContainsKey(uid))
        {
            return loaded[uid];
        }

        var entity = (IEntity)Activator.CreateInstance(dataType);

        dataType.GetProperty(nameof(IEntity.Id))
            .SetValue(entity, uid);

        foreach (var predicate in ClassMap.Predicates.Where(x => x.Key.DeclaringType == dataType))
        {
            if (element.TryGetProperty(predicate.Value.PredicateName, out var value))
            {
                if (predicate.Key.PropertyType == typeof(Uid))
                {
                    Uid id = value.GetString();
                    predicate.Value.SetValue(id, entity);
                }
                else
                {
                    if (predicate.Key.PropertyType.IsAssignableTo(typeof(IEntity)))
                    {
                        predicate.Value.SetValue(ByteString.CopyFromUtf8(value.ToString()).FromJson(predicate.Key.PropertyType, loaded), entity);
                    }
                    else
                    {
                        predicate.Value.SetValue(value.Deserialize(predicate.Key.PropertyType), entity);
                    }
                }
            }
        }

        return entity;
    }

    private partial class JsonClassMap<T> : ClassMap<T> where T : IEntity
    {
        protected override void Map()
        {
            SetType(typeof(T).Name);

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.GetCustomAttribute<JsonPropertyNameAttribute>() is not null &&
                    x.Name != "Id" && x.Name != "DgraphType");

            foreach (var prop in properties)
            {
                var attr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();

                if (prop.PropertyType.IsEnum)
                {
                    if (prop.PropertyType.GetCustomAttribute<FlagsAttribute>(true) is not null)
                    {
                        ListString(prop, attr.Name);
                    }
                    else
                    {
                        String(prop, attr.Name, false, false, false, StringToken.Exact, null);
                    }
                }
                else
                {
                    if (TryGetType(prop.PropertyType, out var dataType))
                    {
                        switch (dataType)
                        {
                            case "uid":
                                HasOne(prop, attr.Name.Replace("~", ""), attr.Name.StartsWith("~"), false);
                                break;

                            case "string":
                                String(prop, attr.Name, false, false, false, StringToken.Term, null);
                                break;

                            case "int":
                                Integer(prop, attr.Name, true);
                                break;

                            case "float":
                                Float(prop, attr.Name, true);
                                break;

                            case "datetime":
                                DateTime(prop, attr.Name, Core.DateTimeToken.Hour, false);
                                break;

                            case "geo":
                                Geo(prop, attr.Name, true, false);
                                break;
                        }
                    }
                    else if (dataType.StartsWith("["))
                    {
                        switch (dataType)
                        {
                            case "[uid]":
                                HasMany(prop, attr.Name);
                                break;

                            case "[string]":
                            case "[int]":
                            case "[float]":
                            case "[datetime]":
                            case "[geo]":
                                var dt = dataType[1..^1];
                                List(prop, dt, attr.Name);
                                break;
                        }
                    }
                }
            }
        }
    }
}
