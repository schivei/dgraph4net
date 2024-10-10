using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Protobuf;
using NetGeo.Json.SystemText;

namespace Dgraph4Net.ActiveRecords;

internal class ClassMappingImpl : IClassMapping
{
    public ConcurrentDictionary<Type, IClassMap> ClassMappings => InternalClassMapping.ClassMappings;

    public ConcurrentBag<Migration> Migrations
    {
        get => InternalClassMapping.Migrations;
        set => InternalClassMapping.Migrations = value;
    }

    public virtual object? FromJson(ByteString bytes, Type type, string param)
    {
        if (bytes.IsEmpty)
            return default;

        var element = GetData(bytes);

        if (element is null)
            return default;

        if (!element.HasValue || !element.Value.TryGetProperty(param ?? "_", out var children))
            return JsonSerializer.Deserialize(bytes.ToStringUtf8(), type);

        return JsonSerializer.Deserialize(children.GetRawText(), type);
    }

    public virtual T? FromJson<T>(string bytes) =>
        FromJson<T>(ByteString.CopyFromUtf8(bytes));

    public virtual T? FromJson<T>(string bytes, string param) =>
        FromJson<T>(ByteString.CopyFromUtf8(bytes), param);

    public virtual T? FromJson<T>(ByteString bytes) =>
        (T?)FromJson(bytes, typeof(T), null);

    public virtual T? FromJson<T>(ByteString bytes, string param) =>
        (T?)FromJson(bytes, typeof(T), param);

    public virtual object? FromJson(ByteString bytes, Type type) =>
        FromJson(bytes, type, null);

    public virtual object? FromJson(string str, Type type) =>
        FromJson(str, type, null);

    public virtual object? FromJson(string str, Type type, string param) =>
        FromJson(ByteString.CopyFromUtf8(str), type, param);

    public virtual void Map()
    {
        SetDefaults();

        InternalClassMapping.Map();
    }

    public virtual void Map(params Assembly[] assemblies)
    {
        SetDefaults();

        InternalClassMapping.Map(assemblies);
    }

    public virtual ByteString ToJson<T>(T entity) where T : IEntity =>
        ByteString.CopyFromUtf8(ToJsonString(entity).Trim());

    public virtual string ToJsonString<T>(T entity) where T : IEntity =>
        Serialize(entity);

    public virtual bool TryMapJson(Type type, out IClassMap? classMap)
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

    public virtual string Serialize(object obj) =>
        JsonSerializer.Serialize(obj);

    public T? Deserialize<T>(string json) =>
        (T)Deserialize(json, typeof(T));

    public virtual object? Deserialize(string json, Type type) =>
        JsonSerializer.Deserialize(json, type);

    public virtual void SetDefaults() =>
        GeoExtensions.SetDefaults();

    private static JsonElement? GetData(ByteString bytes)
    {
        // get data content from json as json
        var json = JsonSerializer.Deserialize<JsonElement?>(bytes.Span);

        if (!json.HasValue || !json.Value.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return json;

        return data;
    }

    private class JsonClassMap<T> : ClassMap<T> where T : AEntity<T>
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
                                HasOne(prop, attr.Name.Replace("~", ""), attr.Name is ['~', ..], false);
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
                    else if (dataType is ['[', ..])
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
