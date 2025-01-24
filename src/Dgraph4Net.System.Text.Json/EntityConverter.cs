using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using NetGeo.Json;

namespace Dgraph4Net;

internal partial class EntityConverter(bool ignoreNulls, bool getOnlyNulls = false, bool convertDefaultToNull = false) : JsonConverter<IEntity>
{
    public bool IgnoreNulls { get; set; } = ignoreNulls;
    public bool GetOnlyNulls { get; set; } = getOnlyNulls;
    public bool ConvertDefaultToNull { get; set; } = convertDefaultToNull;

    public EntityConverter() : this(true) { }

    static EntityConverter()
    {
        IIEntityConverter.Instance ??= typeof(EntityConverter);

        AppContext.SetSwitch("System.Text.Json.Serialization.Metadata", true);
        AppContext.SetSwitch("System.Text.Json.Serialization.Converters", true);
        AppContext.SetSwitch("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", true);

        var jsonOptions = JsonSerializerOptions.Default;

        var field = typeof(JsonSerializerOptions).GetField("_isReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(jsonOptions, false);

        var fieldRef = typeof(JsonSerializerOptions).GetField("_referenceHandler", BindingFlags.Instance | BindingFlags.NonPublic);
        fieldRef?.SetValue(jsonOptions, ReferenceHandler.IgnoreCycles);

        if (!jsonOptions.Converters.Any(x => x is EntityConverter))
            jsonOptions.Converters.Add(new EntityConverter());

        if (!jsonOptions.Converters.Any(x => x is UidConverter))
            jsonOptions.Converters.Add(new UidConverter());

        field.SetValue(jsonOptions, true);
    }

    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsAssignableTo(typeof(IEntity));

    public override IEntity? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null or JsonTokenType.None)
            return default;

        if (Activator.CreateInstance(typeToConvert) is not IEntity entity)
            return default;

        var map = IIEntityConverter.GetPredicates(typeToConvert).FirstOrDefault()?.ClassMap;

        if (map is null)
            return default;

        var obj = JsonSerializer.Deserialize(ref reader, typeof(JsonObject), options) as JsonObject ??
                  throw new InvalidOperationException("Can not deserialize the object");

        var objProperties = obj.ToImmutableDictionary();

        foreach (var (propertyName, value) in obj)
        {
            if (value is null || value.GetValueKind() is JsonValueKind.Undefined or JsonValueKind.Null)
                continue;

            switch (propertyName)
            {
                case "uid":
                    entity.Uid.Replace(value.ToString());
                    continue;
                case "dgraph.type":
                    entity.DgraphType = (value.Deserialize<string[]>(options) ?? []).Distinct().ToArray();
                    continue;
            }

            var predicate = IIEntityConverter.GetPredicate(typeToConvert, propertyName);
            if (predicate is null)
            {
                if (propertyName.Contains('@') || propertyName.Contains('|'))
                {
                    switch (value.GetValueKind())
                    {
                        case JsonValueKind.String:
                            var vstr = value.Deserialize<string>(options);
                            if (DateTimeOffset.TryParse(vstr, out var dto))
                            {
                                entity.SetFacet(propertyName, dto);
                                break;
                            }

                            entity.SetFacet(propertyName, vstr);
                            break;
                        case JsonValueKind.Number:
                            var vdouble = value.Deserialize<double>(options);

                            if (int.TryParse(vdouble.ToString(CultureInfo.InvariantCulture), out var i))
                            {
                                entity.SetFacet(propertyName, i);
                                break;
                            }

                            if (float.TryParse(vdouble.ToString(CultureInfo.InvariantCulture), out var f))
                            {
                                entity.SetFacet(propertyName, f);
                                break;
                            }

                            entity.SetFacet(propertyName, vdouble);
                            break;
                        case JsonValueKind.True:
                            entity.SetFacet(propertyName, true);
                            break;
                        case JsonValueKind.False:
                            entity.SetFacet(propertyName, false);
                            break;
                        case JsonValueKind.Null:
                        case JsonValueKind.Undefined:
                            entity.SetFacet(propertyName, null!);
                            break;
                        case JsonValueKind.Array:
                        case JsonValueKind.Object:
                            break;
                        default:
                            entity.SetFacet(propertyName, value.Deserialize<string>(options));
                            break;
                    }
                }

                continue;
            }

            if (predicate.Property.PropertyType == typeof(Vector<float>))
            {
                var str = value.ToString();
                predicate.SetValue(entity, str.DeserializeVector());
                continue;
            }

            if (predicate.Property.PropertyType.IsAssignableTo(typeof(IEnumerable<IFacetedValue>)))
            {
                var propValueType = predicate.Property.PropertyType.GetInterface("IEnumerable`1")?.GetGenericArguments().FirstOrDefault() ??
                    throw new InvalidOperationException("Can not find the generic type of the target");

                var valueType = ((IFacetedValue)Activator.CreateInstance(propValueType)).ValueType;

                var facets = objProperties
                    .Where(x => x.Key.StartsWith(predicate.PredicateName + '|'))
                    .ToDictionary(x => x.Key.Split('|')[1], x => x.Value.AsObject().ToDictionary());

                var pvalues = value.AsArray().ToArray()
                    .Select((v, i) => v.GetValueKind() is JsonValueKind.Null or JsonValueKind.Undefined ? (i.ToString(), null) : (i.ToString(), v.Deserialize(valueType, options)))
                    .Where(v => v.Item2 is not null);

                var instances = new List<IFacetedValue>();

                foreach (var (pos, val) in pvalues)
                {
                    var vInstance = (IFacetedValue)Activator.CreateInstance(propValueType) ??
                        throw new InvalidOperationException("Can not create the instance of the target");

                    vInstance.Value = val;

                    var pfvalues = facets.SelectMany(f =>
                        f.Value.Select(fv => (f.Key, fv))
                               .Where(fv => fv.fv.Key == pos)
                               .Select(fv => (fv.Key, fv.fv.Value)))
                        .ToDictionary(fv => fv.Key, fv => fv.Value);

                    foreach (var (facetName, fvalue) in pfvalues)
                    {
                        if (fvalue.GetValueKind() is JsonValueKind.Null or JsonValueKind.Undefined)
                            continue;

                        var fstr = fvalue.ToString();

                        object? facetValueInstance = fvalue.GetValueKind() switch
                        {
                            JsonValueKind.String when DateTimeOffset.TryParse(fstr, out var dto) => dto,
                            JsonValueKind.Number when int.TryParse(fstr, out var i) && i.ToString(CultureInfo.InvariantCulture) == fstr => i,
                            JsonValueKind.Number when float.TryParse(fstr, out var f) && f.ToString(CultureInfo.InvariantCulture) == fstr => f,
                            JsonValueKind.Number when long.TryParse(fstr, out var l) && l.ToString(CultureInfo.InvariantCulture) == fstr => l,
                            JsonValueKind.Number => fvalue.Deserialize<double>(options),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => fstr
                        };

                        vInstance.SetFacet(facetName, facetValueInstance);
                    }

                    instances.Add(vInstance);
                }

                object? propInstance = instances;

                if (predicate.Property.PropertyType.Name is [.., '[', ']'])
                {
                    var arr = Array.CreateInstance(propValueType, instances.Count);

                    for (var i = 0; i < instances.Count; i++)
                        arr.SetValue(instances[i], i);

                    propInstance = arr;
                }
                else if (!predicate.Property.PropertyType.IsInterface)
                {
                    var collection = (ICollection)Activator.CreateInstance(predicate.Property.PropertyType);

                    var addMethod = predicate.Property.PropertyType.GetMethod("Add");

                    foreach (var instance in instances)
                        addMethod?.Invoke(collection, [instance]);

                    propInstance = collection;
                }

                predicate.Property.SetValue(entity, propInstance);
                continue;
            }

            if (predicate.Property.PropertyType.IsAssignableTo(typeof(IFacetPredicate)))
            {
                IFacetPredicate? facet;

                if (predicate.Property.PropertyType.Name == "FacetPredicate`2")
                {
                    var gen = predicate.Property.PropertyType.GetGenericArguments();
                    if (gen.Length != 2)
                        throw new InvalidOperationException("The property must have two generic types");

                    var tp = gen.FirstOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the target");

                    var tpe = gen.LastOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the prop");

                    var predicateValue = value.Deserialize(tpe, options);

                    var facetType = predicate.Property.PropertyType.MakeGenericType(tp, tpe);
                    facet = (IFacetPredicate)(Activator.CreateInstance(facetType, entity, predicate.Property, predicateValue) ??
                        throw new InvalidOperationException("Can not create the facet predicate"));
                }
                else
                {
                    var gen = predicate.Property.PropertyType.GetInterfaces().First(p => p.Name == "IFacetPredicate`2")
                        .GetGenericArguments();
                    if (gen.Length != 2)
                        throw new InvalidOperationException("The property must have two generic types");

                    var tpe = gen.LastOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the prop");

                    var predicateValue = value.Deserialize(tpe, options);

                    var facetValue = predicate.Property.GetValue(entity);

                    if (facetValue is null)
                        facet = (IFacetPredicate)(Activator.CreateInstance(predicate.Property.PropertyType, predicateValue) ??
                                                  throw new InvalidOperationException("Can not create the facet predicate"));
                    else
                        facet = (IFacetPredicate)facetValue;

                    facet.PredicateValue = predicateValue;
                }

                predicate.Property.SetValue(entity, facet);
                continue;
            }

            if (predicate.Property.PropertyType.IsAssignableTo(typeof(GeoObject)) && options.Converters.All(x => x.GetType().Name != "GeoObjectConverter"))
            {
                var geoObject = value.Deserialize(predicate.Property.PropertyType, GetGeoOptions(options));

                predicate.Property.SetValue(entity, geoObject);
                continue;
            }

            if (predicate.Property.PropertyType.IsAssignableTo(typeof(IEntity)))
            {
                var newReader = Read(value);

                object? entityValue = Read(ref newReader, predicate.Property.PropertyType, options);

                predicate.Property.SetValue(entity, entityValue);

                continue;
            }

            if (predicate.Property.PropertyType.IsAssignableTo(typeof(IEnumerable<IEntity>)))
            {
                object listValue;

                if (predicate.Property.PropertyType.IsInterface)
                {
                    var list = new List<IEntity>();

                    foreach (var item in value.AsArray())
                    {
                        var newReader = Read(item);

                        if (Read(ref newReader, predicate.Property.PropertyType.GetInterfaces()
                                .First(p => p.Name == "IEnumerable`1").GetGenericArguments()
                                .FirstOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the target"), options) is not { } entityValue)
                            continue;

                        list.Add(entityValue);
                    }

                    listValue = list;
                }
                else if (predicate.Property.PropertyType.Name is [.., '[', ']'])
                {
                    var varr = value.AsArray();
                    var arr = Array.CreateInstance(predicate.Property.PropertyType.GetInterfaces()
                        .First(p => p.Name == "IEnumerable`1")
                        .GetGenericArguments()
                        .FirstOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the target"), varr.Count);

                    var index = 0;
                    foreach (var item in varr)
                    {
                        var newReader = Read(item);

                        if (Read(ref newReader, predicate.Property.PropertyType.GetInterfaces()
                                .First(p => p.Name == "IEnumerable`1")
                                .GetGenericArguments()
                                .FirstOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the target"), options) is not { } entityValue)
                            continue;

                        arr.SetValue(entityValue, index++);
                    }

                    listValue = arr;
                }
                else
                {
                    var list = Activator.CreateInstance(predicate.Property.PropertyType) as ICollection<IEntity>;

                    foreach (var item in value.AsArray())
                    {
                        var newReader = Read(item);

                        if (Read(ref newReader, predicate.Property.PropertyType.GetInterfaces()
                                .First(p => p.Name == "IEnumerable`1")
                                .GetGenericArguments()
                                .FirstOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the target"), options) is not { } entityValue)
                            continue;

                        list.Add(entityValue);
                    }

                    listValue = list;
                }

                predicate.Property.SetValue(entity, listValue);

                continue;
            }

            predicate.Property.SetValue(entity, value.Deserialize(predicate.Property.PropertyType, options));
        }

        return entity;
    }

    private static Utf8JsonReader Read(JsonNode item)
    {
        var json = Encoding.UTF8.GetBytes(item.ToJsonString());
        var itemReader = new Utf8JsonReader(json);
        itemReader.Read();
        return itemReader;
    }

    private static JsonSerializerOptions? s_geoOptions;

    private static JsonSerializerOptions GetGeoOptions(JsonSerializerOptions options)
    {
        if (s_geoOptions is not null)
            return s_geoOptions;

        var convType = Type.GetType("NetGeo.Json.SystemText.GeoObjectConverter, NetGeo.Json")!;
        var converter = (JsonConverter<GeoObject>)Activator.CreateInstance(convType);

        return s_geoOptions = new(options) { Converters = { converter } };
    }

    public override void Write(Utf8JsonWriter writer, IEntity value, JsonSerializerOptions options)
    {
        var predicates = IIEntityConverter.GetPredicates(value.GetType());

        writer.WriteStartObject();

        foreach (var predicate in predicates)
        {
            var pvalue = predicate.Property.GetValue(value);

            switch (pvalue)
            {
                case null when IgnoreNulls:
                    continue;
                case null when GetOnlyNulls:
                    writer.WriteNull(predicate.PredicateName);
                    continue;
            }

            var defaultValue = predicate.Property.PropertyType.IsValueType ? Activator.CreateInstance(predicate.Property.PropertyType) : default;
            if (pvalue == defaultValue && ConvertDefaultToNull)
            {
                writer.WriteNull(predicate.PredicateName);
                continue;
            }

            if (predicate.PredicateName == "uid")
            {
                writer.WriteString("uid", pvalue.ToString());
                continue;
            }

            if (pvalue is Vector<float> vector)
            {
                writer.WriteString(predicate.PredicateName, vector.Serialize());
                continue;
            }

            if (predicate.Property.PropertyType.IsAssignableTo(typeof(IFacetPredicate)))
            {
                var facet = pvalue as IFacetPredicate;

                if (facet?.PredicateValue is null)
                {
                    if (IgnoreNulls || !ConvertDefaultToNull || facet.PredicateValue != defaultValue)
                        continue;

                    writer.WriteNull(predicate.PredicateName);
                    continue;
                }

                writer.WritePropertyName(predicate.PredicateName);
                JsonSerializer.Serialize(writer, facet.PredicateValue, options);
                continue;
            }

            if (predicate.Property.PropertyType.IsAssignableTo(typeof(GeoObject)) &&
                JsonSerializerOptions.Default.Converters.All(x => x.GetType().Name != "GeoObjectConverter"))
            {
                if (pvalue is not GeoObject geoObject)
                {
                    if (IgnoreNulls || (!GetOnlyNulls && !ConvertDefaultToNull))
                        continue;

                    writer.WriteNull(predicate.PredicateName);
                    continue;
                }

                var convType = Type.GetType("NetGeo.Json.SystemText.GeoObjectConverter, NetGeo.Json")!;
                var converter = (JsonConverter<GeoObject>)Activator.CreateInstance(convType);

                writer.WritePropertyName(predicate.PredicateName);
                converter.Write(writer, geoObject, options);
                continue;
            }

            if (pvalue is IEnumerable objects and not IEnumerable<IEntity> and not string && predicate.PredicateName != "dgraph.type")
            {
                if (pvalue is IEnumerable<byte> bytes)
                {
                    writer.WritePropertyName(predicate.PredicateName);
                    writer.WriteBase64StringValue(bytes.ToArray());
                    continue;
                }

                if (pvalue is IEnumerable<IFacetedValue> facetedValues)
                {
                    if (!facetedValues.Any())
                    {
                        if (GetOnlyNulls || !IgnoreNulls)
                            writer.WriteNull(predicate.PredicateName);

                        if (!GetOnlyNulls && !IgnoreNulls && ConvertDefaultToNull)
                        {
                            writer.WriteStartArray(predicate.PredicateName);
                            writer.WriteEndArray();
                        }

                        continue;
                    }

                    var allValues = new List<object?>();
                    var allFacetValues = new Dictionary<string, Dictionary<string, object?>>();
                    var allFacetNames = facetedValues.SelectMany(x => x.Facets.Keys)
                        .ToHashSet();

                    foreach (var (item, i) in facetedValues.Select((item, i) => (item, i)))
                    {
                        allValues.Add(item.Value);

                        foreach (var facetName in allFacetNames)
                        {
                            var key = $"{predicate.PredicateName}|{facetName}";

                            if (item.Facets.TryGetValue(facetName, out var facetValue))
                            {
                                if (!allFacetValues.ContainsKey(facetName))
                                    allFacetValues[key] = [];

                                allFacetValues[key][i.ToString()] = facetValue;
                            }
                        }
                    }

                    writer.WritePropertyName(predicate.PredicateName);
                    JsonSerializer.Serialize(writer, allValues, options);

                    if (allFacetValues.Any(f => f.Value.Count != 0))
                    {
                        foreach (var (key, values) in allFacetValues)
                        {
                            writer.WritePropertyName(key);
                            JsonSerializer.Serialize(writer, values, options);
                        }
                    }

                    continue;
                }

                var data = objects.Cast<object?>();

                if (!data.Any())
                {
                    if (GetOnlyNulls || !IgnoreNulls)
                        writer.WriteNull(predicate.PredicateName);

                    if (!GetOnlyNulls && !IgnoreNulls && ConvertDefaultToNull)
                    {
                        writer.WriteStartArray(predicate.PredicateName);
                        writer.WriteEndArray();
                    }

                    continue;
                }

                writer.WriteStartArray(predicate.PredicateName);

                foreach (var item in data)
                {
                    if (item is null)
                    {
                        if (IgnoreNulls && !GetOnlyNulls)
                            continue;

                        writer.WriteNullValue();
                    }

                    var defaultItemValue = item.GetType().IsValueType ? Activator.CreateInstance(item.GetType()) : default;

                    if (item is string && defaultItemValue is null)
                        defaultItemValue = string.Empty;

                    if (item == defaultItemValue && ConvertDefaultToNull)
                    {
                        writer.WriteNullValue();
                        continue;
                    }

                    JsonSerializer.Serialize(writer, item, options);
                }

                writer.WriteEndArray();
                continue;
            }

            if (pvalue is IEnumerable enumerables and not string && predicate.PredicateName != "dgraph.type")
            {
                writer.WriteStartArray(predicate.PredicateName);

                foreach (var data in enumerables)
                {
                    if (data is null)
                    {
                        if (!IgnoreNulls || GetOnlyNulls)
                            writer.WriteNullValue();

                        continue;
                    }

                    if (data is IEntity entity)
                    {
                        if (entity.Uid.IsEmpty)
                            entity.Uid.Replace(Uid.NewUid());

                        if (entity.Uid.IsConcrete)
                        {
                            writer.WriteStartObject();
                            writer.WritePropertyName("uid");
                            writer.WriteStringValue(entity.Uid.ToString());
                            writer.WriteEndObject();
                            continue;
                        }

                        Write(writer, entity, options);
                    }
                    else
                    {
                        JsonSerializer.Serialize(writer, data, options);
                    }
                }

                writer.WriteEndArray();
                continue;
            }

            if (pvalue is string str)
            {
                writer.WriteString(predicate.PredicateName, str);
                continue;
            }

            if (GetOnlyNulls)
                continue;

            writer.WritePropertyName(predicate.PredicateName);

            if (predicate.PredicateName == "dgraph.type")
            {
                var dtypes = predicate.Property.GetValue(value) as IEnumerable<string> ?? [];

                if (!dtypes.Contains(predicate.ClassMap.DgraphType))
                    dtypes = [.. dtypes, predicate.ClassMap.DgraphType];

                JsonSerializer.Serialize(writer, dtypes.Distinct(), options);
                continue;
            }

            if (pvalue is IEntity reference)
            {
                if (reference.Uid.IsEmpty)
                    reference.Uid.Replace(Uid.NewUid());

                // prevents circular reference and infinite loops
                if (reference.Uid.IsConcrete)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("uid");
                    writer.WriteStringValue(reference.Uid.ToString());
                    writer.WriteEndObject();
                    continue;
                }

                Write(writer, reference, options);
                continue;
            }

            JsonSerializer.Serialize(writer, pvalue, options);
        }

        foreach (var (info, val) in value.Facets)
        {
            writer.WritePropertyName(info.ToString());
            JsonSerializer.Serialize(writer, val, options);
        }

        var facetProps = value.GetType().GetProperties()
            .Where(x => x.GetCustomAttributes().Any(a => a is FacetAttribute));

        foreach (var facetProp in facetProps)
        {
            var facet = facetProp.GetCustomAttributes().OfType<FacetAttribute>().FirstOrDefault();

            if (facet is null)
                continue;

            var predicate = IIEntityConverter.GetPredicate(facet.Property);

            if (predicate is null)
                continue;

            var sep = facet.IsI18N ? '@' : '|';

            var name = $"{predicate.PredicateName}{sep}{facet.Name}";

            writer.WritePropertyName(name);

            var facetValue = facetProp.GetValue(value);

            JsonSerializer.Serialize(writer, facetValue, options);
        }

        writer.WriteEndObject();
    }

    public partial T? Deserialize<T>(string json) where T : IEntity
    {
        var span = Encoding.UTF8.GetBytes(json).AsSpan();
        var reader = new Utf8JsonReader(span);

        return (T?)Read(ref reader, typeof(T), JsonSerializerOptions.Default);
    }

    public partial string Serialize<T>(T entity, bool ignoreNulls, bool getOnlyNulls, bool convertDefaultToNull) where T : IEntity
    {
        IgnoreNulls = ignoreNulls;
        GetOnlyNulls = getOnlyNulls;
        ConvertDefaultToNull = convertDefaultToNull;

        using var writer = new MemoryStream();
        using var jsonwriter = new Utf8JsonWriter(writer);

        Write(jsonwriter, entity, JsonSerializerOptions.Default);

        writer.Seek(0, SeekOrigin.Begin);

        return Encoding.UTF8.GetString(writer.ToArray());
    }

    public static partial string SerializeEntity<T>(T entity, bool ignoreNulls, bool getOnlyNulls, bool convertDefaultToNull) where T : IEntity =>
        new EntityConverter(ignoreNulls, getOnlyNulls, convertDefaultToNull).Serialize(entity, ignoreNulls, getOnlyNulls, convertDefaultToNull);

    public static partial string SerializeEntities<T>(IEnumerable<T> entities, bool ignoreNulls, bool getOnlyNulls, bool convertDefaultToNull) where T : IEntity
    {
        var sb = new StringBuilder();
        sb.Append('[');

        foreach (var entity in entities)
        {
            sb.Append(SerializeEntity(entity, ignoreNulls, getOnlyNulls, convertDefaultToNull));
            sb.Append(',');
        }

        if (sb.Length > 1)
            sb.Remove(sb.Length - 1, 1);

        sb.Append(']');

        return sb.ToString();
    }
}
