using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Protobuf;
using NetGeo.Json.SystemText;

namespace Dgraph4Net.ActiveRecords;

internal class ClassMappingImpl : IClassMapping
{
    protected static IClassMapping Impl { get; private set; }

    public ClassMappingImpl() =>
        Impl = this;

    public ConcurrentDictionary<Type, IClassMap> ClassMappings => InternalClassMapping.ClassMappings;

    public ConcurrentBag<Migration> Migrations
    {
        get => InternalClassMapping.Migrations;
        set => InternalClassMapping.Migrations = value;
    }

    Func<object?, string> IClassMapping.JsonSerializer =>
        obj => JsonSerializer.Serialize(obj);

    Func<string, Type, object?> IClassMapping.JsonDeserializer =>
        (str, type) => JsonSerializer.Deserialize(str, type);

    public virtual object? FromJson(ByteString bytes, Type type, string param)
    {
        if (bytes.IsEmpty)
            return default;

        var element = GetData(bytes);

        if (element is null)
            return default;

        if (!element.HasValue || !element.Value.TryGetProperty(param ?? "_", out var children))
            return Deserialize(bytes.ToStringUtf8(), type);

        return Deserialize(children.GetRawText(), type);
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

    public virtual ByteString ToJson<T>(IEnumerable<T> entities) where T : IEntity =>
        ByteString.CopyFromUtf8(ToJsonString(entities).Trim());

    public virtual string ToJsonString<T>(IEnumerable<T> entities) where T : IEntity =>
        Serialize(entities);

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

    protected virtual IIEntityConverter GetConverter(bool ignoreNulls, bool getOnlyNulls, bool convertDefaultToNull) =>
        new EntityConverter(ignoreNulls, getOnlyNulls, convertDefaultToNull);

    public string Serialize(object obj) =>
        Impl.JsonSerializer(obj);

    public object? Deserialize(string json, Type type) =>
        Impl.JsonDeserializer(json, type);

    public T? Deserialize<T>(string json) =>
        (T)Deserialize(json, typeof(T));

    public virtual void SetDefaults() =>
        GeoExtensions.SetDefaults();

    private static JsonElement? GetData(ByteString bytes)
    {
        // get data content from json as json
        var json = Impl.Deserialize(bytes.ToStringUtf8(), typeof(JsonElement)) as JsonElement?;

        if (!json.HasValue || !json.Value.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return json;

        return data;
    }

    public (ByteString, ByteString) ToJsonBS<T>(T entity, bool dropIfDefault) where T : IEntity
    {
        var set = GetConverter(true, false, false);
        var del = GetConverter(false, true, dropIfDefault);

        var setted = ByteString.CopyFromUtf8(set.Serialize(entity));
        var deleted = ByteString.CopyFromUtf8(del.Serialize(entity));

        return (setted, deleted);
    }

    public (ByteString, ByteString) ToJsonBS<T>(IEnumerable<T> entities, bool dropIfDefault) where T : IEntity
    {
        var set = GetConverter(true, false, false);
        var del = GetConverter(false, true, dropIfDefault);

        var sbSet = new StringBuilder();
        var sbDel = new StringBuilder();

        foreach (var entity in entities)
        {
            sbSet.Append(set.Serialize(entity));
            sbDel.Append(del.Serialize(entity));
        }

        var setted = ByteString.CopyFromUtf8(sbSet.ToString());
        var deleted = ByteString.CopyFromUtf8(sbDel.ToString());

        return (setted, deleted);
    }

    public (ByteString, ByteString) ToNQuads<T>(T entity, bool dropIfNull) where T : IEntity =>
        NQuadsConverter.ToNQuads(entity, dropIfNull);

    public (ByteString, ByteString) ToNQuads<T>(IEnumerable<T> entities, bool dropIfNull) where T : IEntity =>
        NQuadsConverter.ToNQuads(entities, dropIfNull);

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
                        String(prop, attr.Name, false, false, false, StringToken.Exact, false);
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
                                String(prop, attr.Name, false, false, false, StringToken.Term, false);
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
