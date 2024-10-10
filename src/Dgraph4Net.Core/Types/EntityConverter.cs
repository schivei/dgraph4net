using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetGeo.Json;

namespace Dgraph4Net;

internal sealed class EntityConverter : JsonConverter<IEntity>
{
    static EntityConverter()
    {
        AppContext.SetSwitch("System.Text.Json.Serialization.Metadata", true);
        AppContext.SetSwitch("System.Text.Json.Serialization.Converters", true);
        AppContext.SetSwitch("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", true);

        var jsonOptions = JsonSerializerOptions.Default;

        var field = typeof(JsonSerializerOptions).GetField("_isReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);

        field?.SetValue(jsonOptions, false);

        if (!jsonOptions.Converters.Any(x => x is EntityConverter))
            jsonOptions.Converters.Add(new EntityConverter());

        if (!jsonOptions.Converters.Any(x => x is UidConverter))
            jsonOptions.Converters.Add(new UidConverter());

        field.SetValue(jsonOptions, true);
    }

    public static ConcurrentDictionary<PropertyInfo, IPredicate> Predicates { get; } = new();

    public static IPredicate? GetPredicate(Type type, string predicateName) =>
        Predicates.FirstOrDefault(x => x.Value.ClassMap.Type == type && x.Value.PredicateName == predicateName).Value;

    public static IEnumerable<IPredicate> GetPredicates(Type type) =>
        Predicates.Where(x => x.Value.ClassMap.Type == type).Select(x => x.Value);

    public static IPredicate? GetPredicate(PropertyInfo prop) =>
        Predicates.TryGetValue(prop, out var predicate) ? predicate : default;

    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsAssignableTo(typeof(IEntity));

    public override IEntity? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null || reader.TokenType == JsonTokenType.None)
            return default;

        if (Activator.CreateInstance(typeToConvert) is not IEntity entity)
            return default;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject && reader.TokenType != JsonTokenType.None)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            var propertyName = reader.GetString();

            if (propertyName is null)
                continue;

            reader.Read();

            var predicate = GetPredicate(typeToConvert, propertyName);

            if (predicate is null)
            {
                if (propertyName.Contains('@') || propertyName.Contains('|'))
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.String:
                            if (reader.TryGetDateTimeOffset(out var dto))
                            {
                                entity.SetFacet(propertyName, dto);
                                break;
                            }

                            entity.SetFacet(propertyName, reader.GetString());
                            break;
                        case JsonTokenType.Number:
                            if (reader.TryGetInt32(out var i))
                            {
                                entity.SetFacet(propertyName, i);
                                break;
                            }

                            if (reader.TryGetSingle(out var f))
                            {
                                entity.SetFacet(propertyName, f);
                                break;
                            }

                            entity.SetFacet(propertyName, reader.GetDouble());
                            break;
                        case JsonTokenType.True:
                            entity.SetFacet(propertyName, true);
                            break;
                        case JsonTokenType.False:
                            entity.SetFacet(propertyName, false);
                            break;
                        case JsonTokenType.Null:
                        case JsonTokenType.None:
                            entity.SetFacet(propertyName, null!);
                            break;
                        default:
                            entity.SetFacet(propertyName, reader.GetString());
                            break;
                    }
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

                    var predicateValue = JsonSerializer.Deserialize(ref reader, tpe, options);

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

                    var predicateValue = JsonSerializer.Deserialize(ref reader, tpe, options);

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
                !JsonSerializerOptions.Default.Converters.Any(x => x.GetType().Name == "GeoObjectConverter"))
            {
                var convType = Type.GetType("NetGeo.Json.SystemText.GeoObjectConverter, NetGeo.Json")!;
                var converter = (JsonConverter<GeoObject>)Activator.CreateInstance(convType);

                var geoObject = converter.Read(ref reader, predicate.Property.PropertyType, options);

                predicate.Property.SetValue(entity, geoObject);
                continue;
            }

            var value = JsonSerializer.Deserialize(ref reader, predicate.Property.PropertyType, options);

            predicate.Property.SetValue(entity, value);
        }

        return entity;
    }

    public override void Write(Utf8JsonWriter writer, IEntity value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var predicates = GetPredicates(value.GetType());

        writer.WriteStartObject();

        foreach (var predicate in predicates)
        {
            writer.WritePropertyName(predicate.PredicateName);

            if (predicate.PredicateName == "uid")
            {
                var uid = predicate.Property.GetValue(value);

                writer.WriteStringValue(uid?.ToString());
                continue;
            }

            if (predicate.PredicateName == "dgraph.type")
            {
                var dtypes = predicate.Property.GetValue(value) as IEnumerable<string> ?? [];

                if (!dtypes.Contains(predicate.ClassMap.DgraphType))
                    dtypes = [.. dtypes, predicate.ClassMap.DgraphType];

                JsonSerializer.Serialize(writer, dtypes, options);
                continue;
            }

            if (predicate.Property.PropertyType.IsAssignableTo(typeof(IFacetPredicate)))
            {
                var facet = predicate.Property.GetValue(value) as IFacetPredicate;

                if (facet?.PredicateValue is null)
                {
                    writer.WriteNullValue();
                    continue;
                }

                JsonSerializer.Serialize(writer, facet.PredicateValue, options);
                continue;
            }

            if (predicate.Property.PropertyType.IsAssignableTo(typeof(GeoObject)) &&
            !JsonSerializerOptions.Default.Converters.Any(x => x.GetType().Name == "GeoObjectConverter"))
            {
                if (predicate.Property.GetValue(value) is not GeoObject geoObject)
                {
                    writer.WriteNullValue();
                    continue;
                }

                var convType = Type.GetType("NetGeo.Json.SystemText.GeoObjectConverter, NetGeo.Json")!;
                var converter = (JsonConverter<GeoObject>)Activator.CreateInstance(convType);

                converter.Write(writer, geoObject, options);
                continue;
            }

            JsonSerializer.Serialize(writer, predicate.Property.GetValue(value), options);
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

            var predicate = GetPredicate(facet.Property);

            if (predicate is null)
                continue;

            var sep = facet.IsI18n ? '@' : '|';

            var name = $"{predicate.PredicateName}{sep}{facet.Name}";

            writer.WritePropertyName(name);

            var facetValue = facetProp.GetValue(value);

            JsonSerializer.Serialize(writer, facetValue, options);
        }

        writer.WriteEndObject();
    }
}
