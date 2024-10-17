using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text;
using Dgraph4Net.ActiveRecords;
using NetGeo.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ECF = Dgraph4Net.EntityConverter;

namespace Dgraph4Net.Newtonsoft.Json;

internal class EntityConverter(bool ignoreNulls, bool getOnlyNulls = false, bool convertDefaultToNull = false) : JsonConverter, IIEntityConverter
{
    public bool IgnoreNulls { get; set; } = ignoreNulls;
    public bool GetOnlyNulls { get; set; } = getOnlyNulls;
    public bool ConvertDefaultToNull { get; set; } = convertDefaultToNull;

    public EntityConverter() : this(true) { }

    public override bool CanConvert(Type objectType) =>
        objectType.IsAssignableTo(typeof(IEntity));

    private class Setter : IDisposable
    {
        private readonly JsonSerializer _serializer;
        private readonly int _index;
        private readonly JsonConverter _converter;

        public Setter(JsonSerializer serializer, JsonConverter converter)
        {
            _serializer = serializer;
            _converter = converter;
            _index = serializer.Converters.IndexOf(converter);

            serializer.Converters.RemoveAt(_index);
        }

        public void Dispose() => _serializer.Converters.Insert(_index, _converter);
    }

    private Setter setter(JsonSerializer serializer) =>
        new(serializer, this);

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null || reader.TokenType == JsonToken.None)
            reader.Read();

        if (reader.TokenType == JsonToken.Null || reader.TokenType == JsonToken.None)
            return null;

        if (existingValue is not IEntity entity)
            entity = (IEntity)Activator.CreateInstance(objectType)!;

        var obj = serializer.Deserialize<JObject>(reader);

        foreach (var (propertyName, value) in obj)
        {
            if (value.Type == JTokenType.Null || value.Type == JTokenType.Undefined || value.Type == JTokenType.None)
                continue;

            if (propertyName == "uid")
            {
                using (setter(serializer))
                    entity.Uid.Replace(value.ToString());
                continue;
            }

            if (propertyName == "dgraph.type")
            {
                using (setter(serializer))
                {
                    try
                    {
                        entity.DgraphType = [.. (value.ToObject<string[]>() ?? []), .. entity.DgraphType];
                    }
                    catch
                    {
                        entity.DgraphType = [value.ToString()];
                    }
                }
                continue;
            }

            var predicate = ClassMapping.GetPredicate(objectType, propertyName);
            if (predicate is null)
            {
                if (propertyName.Contains('@') || propertyName.Contains('|'))
                {
                    switch (value.Type)
                    {
                        case JTokenType.Date:
                            using (setter(serializer))
                                entity.SetFacet(propertyName, value.ToObject<DateTimeOffset>());
                            break;

                        case JTokenType.Float:
                            double d;

                            using (setter(serializer))
                                d = value.ToObject<double>();

                            if (float.TryParse(d.ToString(), out var f))
                            {
                                entity.SetFacet(propertyName, f);
                                break;
                            }

                            entity.SetFacet(propertyName, d);
                            break;

                        case JTokenType.Integer:
                            using (setter(serializer))
                                entity.SetFacet(propertyName, value.ToObject<int>());
                            break;

                        case JTokenType.Boolean:
                            using (setter(serializer))
                                entity.SetFacet(propertyName, value.ToObject<bool>());
                            break;

                        case JTokenType.Null:
                        case JTokenType.None:
                        case JTokenType.Undefined:
                            using (setter(serializer))
                                entity.SetFacet(propertyName, null);
                            break;

                        default:
                            using (setter(serializer))
                                entity.SetFacet(propertyName, value.ToString());
                            break;
                    }

                    continue;
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

                var facets = obj.Properties()
                    .Where(x => x.Name.StartsWith(predicate.PredicateName + '|'))
                    .ToDictionary(x => x.Name.Split('|')[1], x => x.Value.ToObject<JObject>().Properties().ToDictionary(y => y.Name, y => y.Value));

                var pvalues = value.ToArray()
                    .Select((v, i) => v.Type is JTokenType.Null or JTokenType.None or JTokenType.Undefined ? (i.ToString(), null) : (i.ToString(), v.ToObject(valueType)))
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
                        if (fvalue.Type is JTokenType.Null or JTokenType.None or JTokenType.Undefined)
                            continue;

                        var fstr = fvalue.ToString();

                        object? facetValueInstance = fvalue.Type switch
                        {
                            JTokenType.Date => fvalue.ToObject<DateTimeOffset>(),
                            JTokenType.Float when float.TryParse(fvalue.ToString(), out var f) && f.ToString(CultureInfo.InvariantCulture) == fstr => f,
                            JTokenType.Float => fvalue.ToObject<double>(),
                            JTokenType.Integer when int.TryParse(fvalue.ToString(), out var i) && i.ToString(CultureInfo.InvariantCulture) == fstr => i,
                            JTokenType.Integer => fvalue.ToObject<long>(),
                            JTokenType.Boolean => fvalue.ToObject<bool>(),
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

                    object? predicateValue;
                    using (setter(serializer))
                        predicateValue = value.ToObject(tpe);

                    var facetType = predicate.Property.PropertyType.MakeGenericType(tp, tpe);
                    facet = (IFacetPredicate)(Activator.CreateInstance(facetType, entity, predicate.Property, predicateValue) ??
                        throw new InvalidOperationException("Can not create the facet predicate"))!;
                }
                else
                {
                    var gen = predicate.Property.PropertyType.GetInterfaces().First(p => p.Name == "IFacetPredicate`2")
                        .GetGenericArguments();
                    if (gen.Length != 2)
                        throw new InvalidOperationException("The property must have two generic types");

                    var tp = gen.FirstOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the target");

                    var tpe = gen.LastOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the prop");

                    object? predicateValue;
                    using (setter(serializer))
                        predicateValue = value.ToObject(tpe);

                    var facetValue = predicate.Property.GetValue(entity);

                    if (facetValue is null)
                        facet = (IFacetPredicate)(Activator.CreateInstance(predicate.Property.PropertyType, predicateValue) ??
                            throw new InvalidOperationException("Can not create the facet predicate"))!;
                    else
                        facet = (IFacetPredicate)facetValue;

                    facet.PredicateValue = predicateValue;
                }

                predicate.Property.SetValue(entity, facet);

                continue;
            }

            if (predicate.Property.PropertyType.IsAssignableTo(typeof(GeoObject)) &&
                !serializer.Converters.Any(x => x.GetType().Name == "GeoObjectConverter"))
            {
                var convType = Type.GetType("NetGeo.Json.GeoObjectConverter, NetGeo.Newtonsoft.Json")!;
                var converter = (JsonConverter<GeoObject>)Activator.CreateInstance(convType);

                var currentValue = predicate.Property.GetValue(entity);

                object? geoObject;
                using (setter(serializer))
                    geoObject = converter.ReadJson(value.CreateReader(), predicate.Property.PropertyType, currentValue, serializer);

                predicate.Property.SetValue(entity, geoObject);

                continue;
            }

            if (predicate.Property.PropertyType.IsAssignableTo(typeof(IEntity)))
            {
                var currentValue = predicate.Property.GetValue(entity);

                object? entityValue = ReadJson(value.CreateReader(), predicate.Property.PropertyType, currentValue, serializer);

                predicate.Property.SetValue(entity, entityValue);

                continue;
            }

            if (predicate.Property.PropertyType.IsAssignableTo(typeof(IEnumerable<IEntity>)))
            {
                var currentValue = predicate.Property.GetValue(entity) as IEnumerable<IEntity> ?? [];

                object listValue;

                static JsonReader read(JToken item)
                {
                    var itemReader = item.CreateReader();
                    itemReader.Read();
                    return itemReader;
                }

                if (predicate.Property.PropertyType.IsInterface)
                {
                    var list = new List<IEntity>();

                    foreach (var item in value)
                    {
                        if (ReadJson(read(item), predicate.Property.PropertyType.GetInterfaces().First(p => p.Name == "IEnumerable`1").GetGenericArguments().FirstOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the target"), null, serializer) is not IEntity entityValue)
                            continue;

                        list.Add(entityValue);
                    }

                    listValue = list;
                }
                else if (predicate.Property.PropertyType.Name is [.., '[', ']'])
                {
                    var arr = Array.CreateInstance(predicate.Property.PropertyType.GetInterfaces().First(p => p.Name == "IEnumerable`1").GetGenericArguments().FirstOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the target"), value.Count());

                    var index = 0;
                    foreach (var item in value)
                    {
                        if (ReadJson(read(item), predicate.Property.PropertyType.GetInterfaces().First(p => p.Name == "IEnumerable`1").GetGenericArguments().FirstOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the target"), null, serializer) is not IEntity entityValue)
                            continue;

                        arr.SetValue(entityValue, index++);
                    }

                    listValue = arr;
                }
                else
                {
                    ICollection<IEntity> list = Activator.CreateInstance(predicate.Property.PropertyType) as ICollection<IEntity>;

                    foreach (var item in value)
                    {
                        if (ReadJson(read(item), predicate.Property.PropertyType.GetInterfaces().First(p => p.Name == "IEnumerable`1").GetGenericArguments().FirstOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the target"), null, serializer) is not IEntity entityValue)
                            continue;

                        list.Add(entityValue);
                    }

                    listValue = list;
                }

                predicate.Property.SetValue(entity, listValue);

                continue;
            }

            using (setter(serializer))
                predicate.SetValue(entity, value.ToObject(predicate.Property.PropertyType));
        }

        return entity;
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is null)
            return;

        var entity = (IEntity)value!;

        var predicates = ClassMapping.GetPredicates(value.GetType());

        writer.WriteStartObject();

        foreach (var predicate in predicates)
        {
            var pvalue = predicate.Property.GetValue(value);

            if (pvalue is null && IgnoreNulls)
                continue;

            if (pvalue is null && GetOnlyNulls)
            {
                writer.WriteNull(predicate.PredicateName);
                continue;
            }

            var defaultValue = predicate.Property.PropertyType.IsValueType ? Activator.CreateInstance(predicate.Property.PropertyType) : default;

            if (defaultValue is null && predicate.Property.PropertyType == typeof(string))
                defaultValue = string.Empty;

            if (pvalue == defaultValue && ConvertDefaultToNull)
            {
                writer.WriteNull(predicate.PredicateName);
                continue;
            }

            if (predicate.PredicateName == "uid")
            {
                writer.WritePropertyName("uid");
                writer.WriteValue(pvalue.ToString());
                continue;
            }

            if (pvalue is Vector<float> vector)
            {
                writer.WritePropertyName(predicate.PredicateName);
                writer.WriteValue(vector.Serialize());
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
                serializer.Serialize(writer, facet.PredicateValue);
                continue;
            }

            if (predicate.Property.PropertyType.IsAssignableTo(typeof(GeoObject)) &&
                !serializer.Converters.Any(x => x.GetType().Name == "GeoObjectConverter"))
            {
                if (pvalue is not GeoObject geoObject)
                {
                    if (IgnoreNulls || (!GetOnlyNulls && !ConvertDefaultToNull))
                        continue;

                    writer.WriteNull(predicate.PredicateName);
                    continue;
                }

                var convType = Type.GetType("NetGeo.Json.GeoObjectConverter, NetGeo.Newtonsoft.Json")!;
                var converter = (JsonConverter)Activator.CreateInstance(convType);

                writer.WritePropertyName(predicate.PredicateName);
                converter.WriteJson(writer, geoObject, serializer);
                continue;
            }

            if (pvalue is IEnumerable objects and not IEnumerable<IEntity> and not string && predicate is not TypePredicate)
            {
                if (pvalue is IEnumerable<byte> bytes)
                {
                    writer.WritePropertyName(predicate.PredicateName);
                    writer.WriteValue(Convert.ToBase64String(bytes.ToArray()));
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
                            writer.WritePropertyName(predicate.PredicateName);
                            writer.WriteStartArray();
                            writer.WriteEndArray();
                        }

                        continue;
                    }

                    var allValues = new List<object?>();
                    var allFacetValues = new Dictionary<string, List<object?>>();
                    var allFacetNames = facetedValues.SelectMany(x => x.Facets.Keys)
                        .ToHashSet();

                    foreach (var item in facetedValues)
                    {
                        allValues.Add(item.Value);

                        foreach (var facetName in allFacetNames)
                        {
                            var key = $"{predicate.PredicateName}|{facetName}";

                            if (!allFacetValues.ContainsKey(facetName))
                                allFacetValues[key] = [];

                            if (!item.Facets.TryGetValue(facetName, out var facetValue))
                                allFacetValues[key].Add(null);
                            else
                                allFacetValues[key].Add(facetValue);
                        }
                    }

                    writer.WritePropertyName(predicate.PredicateName);
                    serializer.Serialize(writer, allValues);

                    if (allFacetValues.Any(f => f.Value.Count != 0))
                    {
                        foreach (var (key, values) in allFacetValues)
                        {
                            writer.WritePropertyName(key);
                            serializer.Serialize(writer, values);
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
                        writer.WritePropertyName(predicate.PredicateName);
                        writer.WriteStartArray();
                        writer.WriteEndArray();
                    }

                    continue;
                }

                writer.WritePropertyName(predicate.PredicateName);
                writer.WriteStartArray();

                foreach (var item in data)
                {
                    if (item is null)
                    {
                        if (IgnoreNulls && !GetOnlyNulls)
                            continue;

                        writer.WriteNull();
                    }

                    var defaultItemValue = item.GetType().IsValueType ? Activator.CreateInstance(item.GetType()) : default;

                    if (item is string && defaultItemValue is null)
                        defaultItemValue = string.Empty;

                    if (item == defaultItemValue && ConvertDefaultToNull)
                    {
                        writer.WriteNull();
                        continue;
                    }

                    serializer.Serialize(writer, item);
                }

                writer.WriteEndArray();
                continue;
            }

            if (pvalue is IEnumerable<IEntity> entities)
            {
                writer.WritePropertyName(predicate.PredicateName);
                writer.WriteStartArray();

                foreach (var ent in entities)
                {
                    if (ent is null)
                    {
                        if (!IgnoreNulls || GetOnlyNulls)
                            writer.WriteNull();

                        continue;
                    }

                    if (ent.Uid.IsEmpty)
                        ent.Uid.Replace(Uid.NewUid());

                    // prevents circular reference and infinite loops
                    if (ent.Uid.IsConcrete)
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyName("uid");
                        writer.WriteValue(ent.Uid.ToString());
                        writer.WriteEndObject();
                        continue;
                    }

                    WriteJson(writer, ent, serializer);
                }

                writer.WriteEndArray();
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

                serializer.Serialize(writer, dtypes);
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
                    writer.WriteValue(reference.Uid.ToString());
                    writer.WriteEndObject();
                    continue;
                }

                WriteJson(writer, reference, serializer);
                continue;
            }

            serializer.Serialize(writer, pvalue);
        }

        foreach (var (info, val) in entity.Facets)
        {
            writer.WritePropertyName(info.ToString());
            serializer.Serialize(writer, val);
        }

        var facetProps = value.GetType().GetProperties()
            .Where(x => x.GetCustomAttributes().Any(a => a is FacetAttribute));

        foreach (var facetProp in facetProps)
        {
            var facet = facetProp.GetCustomAttributes().OfType<FacetAttribute>().FirstOrDefault();

            if (facet is null)
                continue;

            var predicate = ECF.GetPredicate(facet.Property);

            if (predicate is null)
                continue;

            var sep = facet.IsI18n ? '@' : '|';

            var name = $"{predicate.PredicateName}{sep}{facet.Name}";

            writer.WritePropertyName(name);
            var facetValue = facetProp.GetValue(value);

            serializer.Serialize(writer, facetValue);
        }

        writer.WriteEndObject();
    }

    public T? Deserialize<T>(string json) where T : IEntity
    {
        using var reader = new JsonTextReader(new StringReader(json));
        return (T?)ReadJson(reader, typeof(T), null, new JsonSerializer());
    }

    public string Serialize<T>(T entity, bool ignoreNulls = true, bool getOnlyNulls = false, bool convertDefaultToNull = false) where T : IEntity
    {
        IgnoreNulls = ignoreNulls;
        GetOnlyNulls = getOnlyNulls;
        ConvertDefaultToNull = convertDefaultToNull;

        var sb = new StringBuilder();
        using var sw = new StringWriter(sb);
        using var writer = new JsonTextWriter(sw);
        WriteJson(writer, entity, new JsonSerializer());
        return sb.ToString();
    }
}

internal static class JsonWriterExtension
{
    public static void WriteNull(this JsonWriter writer, string propertyName)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteNull();
    }
}
