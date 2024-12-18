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

    public ClassMappingImpl()
    {
        Impl = this;

        InitializeFromJsonByteStringMethod();

        AssignToJsonByteStringFunc();

        _ = new EntityConverter();
    }

    private void InitializeFromJsonByteStringMethod()
    {
        if (InternalClassMapping.FromJsonByteStringFunc is not null)
            return;

        var fromJsonByteStringGenericMethod = GetType()
            .GetMethods()
            .FirstOrDefault(m => m is { IsGenericMethod: true, Name: "FromJson" } &&
                                 m.GetParameters().Length == 1 &&
                                 m.GetParameters()[0].ParameterType == typeof(ByteString));

        InternalClassMapping.FromJsonByteStringFunc = fromJsonByteStringGenericMethod;
        InternalClassMapping.FromJsonByteStringFuncInstance = this;
    }

    private void AssignToJsonByteStringFunc()
    {
        if (InternalClassMapping.ToJsonByteStringFunc is not null)
            return;

        var toJsonByteStringGenericMethod = GetType()
            .GetMethods()
            .FirstOrDefault(m => m is { IsGenericMethod: true, Name: "ToJson" } &&
                                 m.GetParameters().Length == 1 &&
                                 m.GetParameters()[0].ParameterType.IsGenericType &&
                                 m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        InternalClassMapping.ToJsonByteStringFunc = toJsonByteStringGenericMethod;
        InternalClassMapping.ToJsonByteStringFuncInstance = this;
    }

    public ConcurrentDictionary<Type, IClassMap> ClassMappings => InternalClassMapping.ClassMappings;

    public ConcurrentBag<Migration> Migrations
    {
        get => InternalClassMapping.Migrations;
        set => InternalClassMapping.Migrations = value;
    }

    Func<object?, string> IClassMapping.JsonSerializer =>
        obj =>
            JsonSerializer.Serialize(obj, options: new(JsonSerializerOptions.Default)
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                IgnoreReadOnlyFields = true,
                IgnoreReadOnlyProperties = true
            });

    Func<string, Type, object?> IClassMapping.JsonDeserializer =>
        (str, type) => JsonSerializer.Deserialize(str, type);

    public virtual object? FromJson(ByteString bytes, Type type, string? param) =>
        bytes.IsEmpty
            ? default
            : GetData(bytes) is { } element
                ? Deserialize(
                    !element.TryGetProperty(param ?? "_", out var children)
                        ? bytes.ToStringUtf8()
                        : children.GetRawText(), type)
                : default;

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

        if (Activator.CreateInstance(mapType) is not IClassMap map)
            return false;

        classMap = map;
        ClassMappings.TryAdd(type, map);

        map.Start();
        map.Finish();

        return true;

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

    public (ByteString, ByteString) ToJsonBs<T>(T entity, bool dropIfDefault) where T : IEntity
    {
        var set = GetConverter(true, false, false);
        var del = GetConverter(false, true, dropIfDefault);

        var sets = ByteString.CopyFromUtf8(set.Serialize(entity));
        var deletes = ByteString.CopyFromUtf8(del.Serialize(entity));

        return (sets, deletes);
    }

    public (ByteString, ByteString) ToJsonBs<T>(IEnumerable<T> entities, bool dropIfDefault) where T : IEntity
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

        var sets = ByteString.CopyFromUtf8(sbSet.ToString());
        var deletes = ByteString.CopyFromUtf8(sbDel.ToString());

        return (sets, deletes);
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
                                HasOne(prop, attr.Name.Replace("~", ""), attr.Name is ['~', ..]);
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
