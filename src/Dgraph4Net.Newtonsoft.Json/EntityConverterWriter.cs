using System.Collections;
using NetGeo.Json;
using System.Numerics;
using System.Reflection;
using Dgraph4Net.ActiveRecords;
using Newtonsoft.Json;

namespace Dgraph4Net;

internal class EntityConverterWriter(bool ignoreNulls, bool getOnlyNulls, bool convertDefaultToNull)
{
    private bool TryWriteNull(object? ent, JsonWriter writer)
    {
        if (ent is not null)
            return false;

        if (!ignoreNulls || getOnlyNulls)
            writer.WriteNull();

        return true;
    }

    private static bool WriteNullAndReturnTrue(JsonWriter writer, string predicateName)
    {
        writer.WriteNull(predicateName);
        return false;
    }

    private bool TryGetPredicateValue(JsonWriter writer, IEntity entity, IPredicate predicate, out object predicateValue) =>
        (predicateValue = predicate.Property.GetValue(entity)) switch
        {
            null when getOnlyNulls => WriteNullAndReturnTrue(writer, predicate.PredicateName),
            null => !ignoreNulls,
            _ => true
        };

    private static object GetDefaultPredicateValue(IPredicate predicate) =>
        predicate.Property.PropertyType.IsValueType
            ? Activator.CreateInstance(predicate.Property.PropertyType)
            : predicate.Property.PropertyType == typeof(string)
                ? string.Empty
                : default;

    private bool TryWriteNull(IPredicate predicate, object predicateValue, object defaultValue, JsonWriter writer) =>
        predicateValue == defaultValue && convertDefaultToNull && WriteNullAndReturnTrue(writer, predicate.PredicateName);

    private static bool TryWriteUid(IPredicate predicate, object predicateValue, JsonWriter writer)
    {
        if (predicate.PredicateName != "uid")
            return false;

        writer.WritePropertyName("uid");
        writer.WriteValue(predicateValue.ToString());
        return true;
    }

    private static bool TryWriteFloat32Vector(IPredicate predicate, object predicateValue, JsonWriter writer)
    {
        if (predicateValue is not Vector<float> vector)
            return false;

        writer.WritePropertyName(predicate.PredicateName);
        writer.WriteValue(vector.Serialize());
        return true;
    }

    private bool TryWriteFacetPredicate(IPredicate predicate, object predicateValue, JsonWriter writer, JsonSerializer serializer)
    {
        if (!predicate.Property.PropertyType.IsAssignableTo(typeof(IFacetPredicate)))
            return false;

        var facet = predicateValue as IFacetPredicate;

        if (facet?.PredicateValue is null)
        {
            if (ignoreNulls || !convertDefaultToNull || facet.PredicateValue != GetDefaultPredicateValue(predicate))
                return true;

            writer.WriteNull(predicate.PredicateName);
            return true;
        }

        writer.WritePropertyName(predicate.PredicateName);
        serializer.Serialize(writer, facet.PredicateValue);
        return true;
    }

    private bool TryWriteGeoObject(IPredicate predicate, object predicateValue, JsonWriter writer, JsonSerializer serializer)
    {
        if (!predicate.Property.PropertyType.IsAssignableTo(typeof(GeoObject)))
            return false;

        if (predicateValue is not GeoObject geoObject)
        {
            if (ignoreNulls || (!getOnlyNulls && !convertDefaultToNull))
                return true;

            writer.WriteNull(predicate.PredicateName);
            return true;
        }

        var convType = Type.GetType("NetGeo.Json.GeoObjectConverter, NetGeo.Newtonsoft.Json")!;
        var converter = (JsonConverter)Activator.CreateInstance(convType);

        writer.WritePropertyName(predicate.PredicateName);
        converter.WriteJson(writer, geoObject, serializer);
        return true;
    }

    private static bool TryWriteBytes(IPredicate predicate, object predicateValue, JsonWriter writer)
    {
        if (predicateValue is not IEnumerable<byte> bytes)
            return false;

        writer.WritePropertyName(predicate.PredicateName);
        writer.WriteValue(Convert.ToBase64String(bytes.ToArray()));
        return true;
    }

    private bool TryWriteFacetedValue(IPredicate predicate, object predicateValue, JsonWriter writer, JsonSerializer serializer)
    {
        if (predicateValue is not IEnumerable<IFacetedValue> facetedValues)
            return false;

        if (!facetedValues.Any())
        {
            if (getOnlyNulls || !ignoreNulls)
                writer.WriteNull(predicate.PredicateName);

            if (getOnlyNulls || ignoreNulls || !convertDefaultToNull)
                return true;

            writer.WritePropertyName(predicate.PredicateName);
            writer.WriteStartArray();
            writer.WriteEndArray();

            return true;
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

                allFacetValues[key].Add(!item.Facets.TryGetValue(facetName, out var facetValue)
                    ? null
                    : facetValue);
            }
        }

        writer.WritePropertyName(predicate.PredicateName);
        serializer.Serialize(writer, allValues);

        if (allFacetValues.All(f => f.Value.Count == 0))
            return true;

        foreach (var (key, values) in allFacetValues)
        {
            writer.WritePropertyName(key);
            serializer.Serialize(writer, values);
        }

        return true;
    }

    private bool TryWriteEmptyData(IPredicate predicate, IEnumerable<object?> data, JsonWriter writer)
    {
        if (data.Any())
            return false;

        if (getOnlyNulls || !ignoreNulls)
            writer.WriteNull(predicate.PredicateName);

        if (getOnlyNulls || ignoreNulls || !convertDefaultToNull)
            return true;
        
        writer.WritePropertyName(predicate.PredicateName);
        writer.WriteStartArray();
        writer.WriteEndArray();

        return true;
    }
    
    private bool TryWriteEnumerations(IPredicate predicate, object predicateValue, JsonWriter writer, JsonSerializer serializer)
    {
        if (predicateValue is not (IEnumerable objects and not IEnumerable<IEntity> and not string) ||
            predicate is TypePredicate)
            return false;

        if (TryWriteBytes(predicate, predicateValue, writer))
            return true;

        if (TryWriteFacetedValue(predicate, predicateValue, writer, serializer))
            return true;

        var data = objects.Cast<object?>();

        if (TryWriteEmptyData(predicate, data, writer))
            return true;

        writer.WritePropertyName(predicate.PredicateName);
        writer.WriteStartArray();

        foreach (var item in data)
        {
            if (item is null)
            {
                if (ignoreNulls && !getOnlyNulls)
                    continue;

                writer.WriteNull();
            }

            var defaultItemValue = item.GetType().IsValueType ? Activator.CreateInstance(item.GetType()) : default;

            if (item is string && defaultItemValue is null)
                defaultItemValue = string.Empty;

            if (item == defaultItemValue && convertDefaultToNull)
            {
                writer.WriteNull();
                continue;
            }

            serializer.Serialize(writer, item);
        }

        writer.WriteEndArray();
        return true;

    }

    private bool TryWriteEntities(IPredicate predicate, object predicateValue, JsonWriter writer,
        JsonSerializer serializer)
    {
        if (predicateValue is not IEnumerable<IEntity> entities)
            return false;
        
        writer.WritePropertyName(predicate.PredicateName);
        writer.WriteStartArray();

        foreach (var ent in entities)
        {
            if (TryWriteNull(ent, writer))
                continue;

            if (ent.Uid.IsEmpty)
                ent.Uid.Replace(Uid.NewUid());

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
        return true;
    }

    public void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value is not IEntity entity)
            return;

        var predicates = ClassMapping.GetPredicates(value.GetType());

        writer.WriteStartObject();

        foreach (var predicate in predicates)
        {
            if (!TryGetPredicateValue(writer, entity, predicate, out var predicateValue))
                continue;

            var defaultValue = GetDefaultPredicateValue(predicate);

            if (TryWriteNull(predicate, predicateValue, defaultValue, writer))
                continue;

            if (TryWriteUid(predicate, predicateValue, writer))
                continue;

            if (TryWriteFloat32Vector(predicate, predicateValue, writer))
                continue;

            if (TryWriteFacetPredicate(predicate, predicateValue, writer, serializer))
                continue;

            if (TryWriteGeoObject(predicate, predicateValue, writer, serializer))
                continue;

            if (TryWriteEnumerations(predicate, predicateValue, writer, serializer))
                continue;

            if (TryWriteEntities(predicate, predicateValue, writer, serializer))
                continue;

            if (getOnlyNulls)
                continue;

            writer.WritePropertyName(predicate.PredicateName);

            if (predicate.PredicateName == "dgraph.type")
            {
                var dTypes = predicate.Property.GetValue(value) as IEnumerable<string> ?? [];

                if (!dTypes.Contains(predicate.ClassMap.DgraphType))
                    dTypes = [.. dTypes, predicate.ClassMap.DgraphType];

                serializer.Serialize(writer, dTypes.Distinct());
                continue;
            }

            if (predicateValue is IEntity reference)
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

            serializer.Serialize(writer, predicateValue);
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

            var predicate = IIEntityConverter.GetPredicate(facet.Property);

            if (predicate is null)
                continue;

            var sep = facet.IsI18N ? '@' : '|';

            var name = $"{predicate.PredicateName}{sep}{facet.Name}";

            writer.WritePropertyName(name);
            var facetValue = facetProp.GetValue(value);

            serializer.Serialize(writer, facetValue);
        }

        writer.WriteEndObject();
    }
}
