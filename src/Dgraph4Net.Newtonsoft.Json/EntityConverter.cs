using System.Reflection;
using Dgraph4Net.ActiveRecords;
using NetGeo.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ECF = Dgraph4Net.EntityConverter;

namespace Dgraph4Net.Newtonsoft.Json;

internal class EntityConverter : JsonConverter
{
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
                    entity.Uid = new Uid(value.ToString());
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
        ArgumentNullException.ThrowIfNull(value);

        var entity = (IEntity)value!;

        var predicates = ClassMapping.GetPredicates(value.GetType());

        writer.WriteStartObject();

        foreach (var predicate in predicates)
        {
            writer.WritePropertyName(predicate.PredicateName);

            if (predicate.PredicateName == "uid")
            {
                var uid = predicate.Property.GetValue(value);

                writer.WriteValue(uid?.ToString());
                continue;
            }

            if (predicate.PredicateName == "dgraph.type")
            {
                var dtypes = predicate.Property.GetValue(value) as IEnumerable<string> ?? [];

                if (!dtypes.Contains(predicate.ClassMap.DgraphType))
                    dtypes = [.. dtypes, predicate.ClassMap.DgraphType];

                serializer.Serialize(writer, dtypes);
                continue;
            }

            if (predicate.Property.PropertyType.IsAssignableTo(typeof(IFacetPredicate)))
            {
                var facet = predicate.Property.GetValue(value) as IFacetPredicate;

                if (facet?.PredicateValue is null)
                {
                    writer.WriteNull();
                    continue;
                }

                serializer.Serialize(writer, facet.PredicateValue);
                continue;
            }

            if (predicate.Property.PropertyType.IsAssignableTo(typeof(GeoObject)) &&
                !serializer.Converters.Any(x => x.GetType().Name == "GeoObjectConverter"))
            {
                if (predicate.Property.GetValue(value) is not GeoObject geoObject)
                {
                    writer.WriteNull();
                    continue;
                }

                var convType = Type.GetType("NetGeo.Json.GeoObjectConverter, NetGeo.Newtonsoft.Json")!;
                var converter = (JsonConverter)Activator.CreateInstance(convType);

                converter.WriteJson(writer, geoObject, serializer);
                continue;
            }

            serializer.Serialize(writer, predicate.Property.GetValue(value));
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
}
