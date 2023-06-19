using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json;
using Google.Protobuf;
using NetGeo.Json.SystemText;
using NetGeo.Json;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Dgraph4Net.ActiveRecords;

internal class ClassMappingImpl : IClassMapping
{
    public ConcurrentDictionary<Type, IClassMap> ClassMappings => InternalClassMapping.ClassMappings;

    public ConcurrentBag<Migration> Migrations
    {
        get => InternalClassMapping.Migrations;
        set => InternalClassMapping.Migrations = value;
    }

    public virtual object? FromJson(ByteString bytes, Type type, string param) =>
        FromJsonBS(bytes, type, param);

    public virtual T? FromJson<T>(string bytes) =>
        FromJson<T>(ByteString.CopyFromUtf8(bytes));

    public virtual T? FromJson<T>(string bytes, string param) =>
        FromJson<T>(ByteString.CopyFromUtf8(bytes), param);

    public virtual T? FromJson<T>(ByteString bytes) =>
        (T?)FromJsonBS(bytes, typeof(T), null);

    public virtual T? FromJson<T>(ByteString bytes, string param) =>
        (T?)FromJsonBS(bytes, typeof(T), param);

    public virtual object? FromJson(ByteString bytes, Type type) =>
        FromJsonBS(bytes, type, null);

    public virtual object? FromJson(string str, Type type) =>
        FromJson(str, type, null);

    public virtual object? FromJson(string str, Type type, string param) =>
        FromJson(ByteString.CopyFromUtf8(str), type, param);

    public virtual object? FromJsonBS(ByteString bytes, Type type, string param)
    {
        if (bytes.IsEmpty)
            return default;

        var loaded = new Dictionary<Uid, object>();

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

            return DeserializeFromJsonBS(bytes, param, dataType, list, loaded, type);
        }
        else
        {
            return Deserialize(bytes.ToStringUtf8(), type);
        }
    }

    public virtual void Map() =>
        InternalClassMapping.Map();

    public virtual void Map(params Assembly[] assemblies) =>
        InternalClassMapping.Map(assemblies);

    public virtual ByteString ToJson<T>(T entity, bool deep = false, bool doNotPropagateNulls = false) where T : IEntity =>
        ByteString.CopyFromUtf8(ToJson(entity, new(), deep, doNotPropagateNulls).Trim());

    public virtual string ToJson<T>(T entity, HashSet<IEntity> mapped, bool deep, bool doNotPropagateNulls = false) where T : IEntity
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

                            json.Append(ToJson(edgeValue, mapped, deep, doNotPropagateNulls)).Append(',');
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
                                    return ToJson(item, mapped, deep, doNotPropagateNulls);
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

    public virtual string ToJsonString<T>(T entity, bool deep = false, bool doNotPropagateNulls = false) where T : IEntity =>
        ToJson(entity, deep, doNotPropagateNulls).ToStringUtf8();

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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1163:Unused parameter.", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    private object? DeserializeFromJsonBS(ByteString bytes, string param, Type dataType, IList? list = null, Dictionary<Uid, object> loaded = default!, Type? type = null)
    {
        loaded ??= new();

        var element = GetData(bytes);

        if (!element.HasValue || !element.Value.TryGetProperty(param ?? "_", out var children) || children.ValueKind != JsonValueKind.Array)
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

        return FromJson(ByteString.CopyFromUtf8(JsonSerializer.Serialize(children)), type, loaded);
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

    private object? DeserializeFromJson(ByteString bytes, Type dataType, Dictionary<Uid, object> loaded)
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
                        predicate.Value.SetValue(FromJson(ByteString.CopyFromUtf8(value.ToString()), type: predicate.Key.PropertyType, loaded: loaded), entity);
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

    public virtual object? FromJson(ByteString bytes, Type type, Dictionary<Uid, object> loaded)
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

    private class JsonClassMap<T> : ClassMap<T> where T : IEntity
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
