using System.Collections;
using NetGeo.Json;
using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using Dgraph4Net.ActiveRecords;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Dgraph4Net;

internal class EntityConverterReader(Func<JsonSerializer, Setter> getSetter)
{
    private static bool CanRead(JsonReader reader)
    {
        if (reader.TokenType is JsonToken.Null or JsonToken.None)
            reader.Read();

        return reader.TokenType is not (JsonToken.Null or JsonToken.None);
    }
    
    private static IEntity CreateEntity(Type objectType, object? existingValue)
    {
        return existingValue as IEntity ??
               (IEntity)Activator.CreateInstance(objectType)!;
    }

    private bool TryReadUuid(string propertyName, JsonSerializer serializer, IEntity entity, JToken value)
    {
        if (propertyName != "uid")
            return false;

        using (getSetter(serializer))
            entity.Uid.Replace(value.ToString());

        return true;
    }

    private bool TryReadType(string propertyName, JsonSerializer serializer, IEntity entity, JToken value)
    {
        if (propertyName != "dgraph.type")
            return false;

        using (getSetter(serializer))
        {
            IEnumerable<string> types = [.. value.ToObject<string[]>() ?? [], .. entity.DgraphType];
            entity.DgraphType = types.Distinct().ToArray();
        }

        return true;
    }

    private bool TryGetPredicate(Type objectType, string propertyName, JsonSerializer serializer, IEntity entity, JToken value, out IPredicate predicate)
    {
        predicate = ClassMapping.GetPredicate(objectType, propertyName);
        var isNotNull = (object?)predicate is not null;

        if (isNotNull)
            ReadFacet(propertyName, serializer, entity, value);

        return isNotNull;
    }

    private void ReadFacet(string propertyName, JsonSerializer serializer, IEntity entity, JToken value)
    {
        if (!propertyName.Contains('@') && !propertyName.Contains('|'))
            return;

        switch (value.Type)
        {
            case JTokenType.Date:
                using (getSetter(serializer))
                    entity.SetFacet(propertyName, value.ToObject<DateTimeOffset>());
                break;

            case JTokenType.Float:
                double d;

                using (getSetter(serializer))
                    d = value.ToObject<double>();

                if (float.TryParse(d.ToString(CultureInfo.InvariantCulture), out var f))
                {
                    entity.SetFacet(propertyName, f);
                    break;
                }

                entity.SetFacet(propertyName, d);
                break;

            case JTokenType.Integer:
                using (getSetter(serializer))
                    entity.SetFacet(propertyName, value.ToObject<int>());
                break;

            case JTokenType.Boolean:
                using (getSetter(serializer))
                    entity.SetFacet(propertyName, value.ToObject<bool>());
                break;

            case JTokenType.Null:
            case JTokenType.None:
            case JTokenType.Undefined:
                using (getSetter(serializer))
                    entity.SetFacet(propertyName, null);
                break;

            default:
                using (getSetter(serializer))
                    entity.SetFacet(propertyName, value.ToString());
                break;
        }
    }

    private static bool TryReadFloat32Vector(IPredicate predicate, IEntity entity, JToken value)
    {
        if (predicate.Property.PropertyType != typeof(Vector<float>))
            return false;

        var str = value.ToString();
        predicate.SetValue(entity, str.DeserializeVector());

        return true;
    }

    private static IEnumerable<IFacetedValue> ReadFacetedValues(IEnumerable<(string, object?)> predicateValues,
        Type propValueType, Dictionary<string, Dictionary<string, JToken>> facets)
    {
        foreach (var (pos, val) in predicateValues)
        {
            if (Activator.CreateInstance(propValueType) is not IFacetedValue vInstance)
                continue;

            vInstance.Value = val;

            var predicateFacetValues = facets.SelectMany(f =>
                    f.Value.Select(fv => (f.Key, fv))
                        .Where(fv =>
                            fv.fv.Key == pos &&
                            fv.fv.Value.Type is not (JTokenType.Null or JTokenType.None or JTokenType.Undefined))
                        .Select(fv => (fv.Key, fv.fv.Value)))
                .ToDictionary(fv => fv.Key, fv => fv.Value)
                .Select(fv => (fv.Key, fv.Value, fv.Value.ToString()))
                .ToImmutableArray();

            foreach (var (facetName, facetValue, facetString) in predicateFacetValues)
            {
                vInstance.SetFacet(facetName, facetValue.Type switch
                {
                    JTokenType.Date => facetValue.ToObject<DateTimeOffset>(),
                    JTokenType.Float when float.TryParse(facetValue.ToString(), out var f) && f.ToString(CultureInfo.InvariantCulture) == facetString => f,
                    JTokenType.Float => facetValue.ToObject<double>(),
                    JTokenType.Integer when int.TryParse(facetValue.ToString(), out var i) && i.ToString(CultureInfo.InvariantCulture) == facetString => i,
                    JTokenType.Integer => facetValue.ToObject<long>(),
                    JTokenType.Boolean => facetValue.ToObject<bool>(),
                    _ => facetString
                });
            }

            yield return vInstance;
        }
    }

    private static void ReadPropertyArray(IPredicate predicate, Type propertyType, ImmutableArray<IFacetedValue> instances, out object properties)
    {
        properties = instances;

        if (!predicate.Property.PropertyType.IsArray)
            return;

        var arr = Array.CreateInstance(propertyType, instances.Length);

        for (var i = 0; i < instances.Length; i++)
            arr.SetValue(instances[i], i);

        properties = arr;
    }

    private static void ReadPropertyEnumerable(IPredicate predicate, ImmutableArray<IFacetedValue> instances, ref object properties)
    {
        if (!predicate.Property.PropertyType.IsAssignableTo(typeof(ICollection)) || predicate.Property.PropertyType.IsInterface)
            return;

        var collection = (ICollection)Activator.CreateInstance(predicate.Property.PropertyType);

        var addMethod = predicate.Property.PropertyType.GetMethod("Add");

        foreach (var instance in instances)
            addMethod?.Invoke(collection, [instance]);

        properties = collection;
    }

    private static bool TryReadFacetedValues(IPredicate predicate, IEntity entity, JObject obj, JToken value)
    {
        if (!predicate.Property.PropertyType.IsAssignableTo(typeof(IEnumerable<IFacetedValue>)))
            return false;

        var propValueType = predicate.Property.PropertyType.GetInterface("IEnumerable`1")?.GetGenericArguments().FirstOrDefault() ??
                    throw new InvalidOperationException("Can not find the generic type of the target");

        var valueType = ((IFacetedValue)Activator.CreateInstance(propValueType)).ValueType;

        var facets = obj.Properties()
            .Where(x => x.Name.StartsWith(predicate.PredicateName + '|'))
            .ToDictionary(x => x.Name.Split('|')[1], x => x.Value.ToObject<JObject>().Properties().ToDictionary(y => y.Name, y => y.Value));

        var predicateValues = value.ToArray()
            .Select((v, i) => v.Type is JTokenType.Null or JTokenType.None or JTokenType.Undefined ? (i.ToString(), null) : (i.ToString(), v.ToObject(valueType)))
            .Where(v => v.Item2 is not null);

        var instances = ReadFacetedValues(predicateValues, propValueType, facets).ToImmutableArray();

        ReadPropertyArray(predicate, propValueType, instances, out var propInstance);
        ReadPropertyEnumerable(predicate, instances, ref propInstance);

        predicate.Property.SetValue(entity, propInstance);
        return true;
    }

    private IFacetPredicate? ReadFacetPredicateGeneric(IPredicate predicate, JsonSerializer serializer, IEntity entity, JToken value)
    {
        if (predicate.Property.PropertyType.Name != "FacetPredicate`2")
            return null;

        var gen = predicate.Property.PropertyType.GetGenericArguments();
        if (gen.Length != 2)
            throw new InvalidOperationException("The property must have two generic types");

        var tp = gen.FirstOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the target");

        var tpe = gen.LastOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the prop");

        object? predicateValue;
        using (getSetter(serializer))
            predicateValue = value.ToObject(tpe);

        var facetType = predicate.Property.PropertyType.MakeGenericType(tp, tpe);

        return Activator.CreateInstance(facetType, entity, predicate.Property, predicateValue) as IFacetPredicate;
    }

    private IFacetPredicate ReadFacetPredicate(IFacetPredicate? facet, IPredicate predicate, JsonSerializer serializer, IEntity entity, JToken value)
    {
        if (facet is not null)
            return facet;

        var gen = predicate.Property.PropertyType.GetInterfaces().First(p => p.Name == "IFacetPredicate`2")
            .GetGenericArguments();

        if (gen.Length != 2)
            throw new InvalidOperationException("The property must have two generic types");

        var tpe = gen.LastOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the prop");

        object? predicateValue;
        using (getSetter(serializer))
            predicateValue = value.ToObject(tpe);

        var facetValue = predicate.Property.GetValue(entity);

        if (facetValue is null)
            facet = (IFacetPredicate)(Activator.CreateInstance(predicate.Property.PropertyType, predicateValue) ??
                                      throw new InvalidOperationException("Can not create the facet predicate"));
        else
            facet = (IFacetPredicate)facetValue;

        facet.PredicateValue = predicateValue;

        return facet;
    }

    private bool TryReadFacetPredicate(JsonSerializer serializer, IPredicate predicate, IEntity entity, JToken value)
    {
        if (!predicate.Property.PropertyType.IsAssignableTo(typeof(IFacetPredicate)))
            return false;

        var facet = ReadFacetPredicateGeneric(predicate, serializer, entity, value);
        facet = ReadFacetPredicate(facet, predicate, serializer, entity, value);

        predicate.Property.SetValue(entity, facet);
        return true;
    }

    private bool TryReadGeoObject(JsonSerializer serializer, IPredicate predicate, IEntity entity, JToken value)
    {
        if (!predicate.Property.PropertyType.IsAssignableTo(typeof(GeoObject)))
            return false;

        var convType = Type.GetType("NetGeo.Json.GeoObjectConverter, NetGeo.Newtonsoft.Json")!;
        var converter = (JsonConverter<GeoObject>)Activator.CreateInstance(convType);

        var currentValue = predicate.Property.GetValue(entity);

        object? geoObject;
        using (getSetter(serializer))
            geoObject = converter.ReadJson(value.CreateReader(), predicate.Property.PropertyType, currentValue, serializer);

        predicate.Property.SetValue(entity, geoObject);

        return true;
    }

    private bool TryReadIEntity(JsonSerializer serializer, IPredicate predicate, IEntity entity, JToken value)
    {
        if (!predicate.Property.PropertyType.IsAssignableTo(typeof(IEntity)))
            return false;

        var currentValue = predicate.Property.GetValue(entity);

        var entityValue = ReadJson(value.CreateReader(), predicate.Property.PropertyType, currentValue, serializer);

        predicate.Property.SetValue(entity, entityValue);
        return true;
    }

    private static JsonReader ReadItem(JToken item)
    {
        var itemReader = item.CreateReader();
        itemReader.Read();
        return itemReader;
    }

    private IEnumerable<IEntity> ReadIEntityEnumerableInterface(JsonSerializer serializer, IPredicate predicate, JToken value)
    {
        foreach (var item in value)
        {
            var valueType =
                predicate.Property.PropertyType.GetInterfaces().First(p => p.Name == "IEnumerable`1")
                    .GetGenericArguments().FirstOrDefault() ??
                throw new InvalidOperationException("Can not find the generic type of the target");

            if (ReadJson(ReadItem(item), valueType, null, serializer) is not IEntity entityValue)
                continue;

            yield return entityValue;
        }
    }

    private Array ReadIEntityEnumerableArray(JsonSerializer serializer, IPredicate predicate, JToken value)
    {
        var propertyType =
            predicate.Property.PropertyType.GetInterfaces().First(p => p.Name == "IEnumerable`1").GetGenericArguments()
                .FirstOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the target");

        var arr = Array.CreateInstance(propertyType, value.Count());

        var index = 0;
        foreach (var item in value)
        {
            if (ReadJson(ReadItem(item), predicate.Property.PropertyType.GetInterfaces().First(p => p.Name == "IEnumerable`1").GetGenericArguments().FirstOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the target"), null, serializer) is not IEntity entityValue)
                continue;

            arr.SetValue(entityValue, index++);
        }

        return arr;
    }

    private ICollection<IEntity> ReadIEntityEnumerableCollection(JsonSerializer serializer, IPredicate predicate, JToken value)
    {
        var list = Activator.CreateInstance(predicate.Property.PropertyType) as ICollection<IEntity>;

        foreach (var item in value)
        {
            if (ReadJson(ReadItem(item), predicate.Property.PropertyType.GetInterfaces().First(p => p.Name == "IEnumerable`1").GetGenericArguments().FirstOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the target"), null, serializer) is not IEntity entityValue)
                continue;

            list.Add(entityValue);
        }

        return list;
    }

    private bool TryReadIEntityEnumerable(JsonSerializer serializer, IPredicate predicate, IEntity entity, JToken value)
    {
        if (!predicate.Property.PropertyType.IsAssignableTo(typeof(IEnumerable<IEntity>)))
            return false;

        object listValue;

        if (predicate.Property.PropertyType.IsInterface)
            listValue = ReadIEntityEnumerableInterface(serializer, predicate, value).ToList();
        else if (predicate.Property.PropertyType.Name is [.., '[', ']'])
            listValue = ReadIEntityEnumerableArray(serializer, predicate, value);
        else
            listValue = ReadIEntityEnumerableCollection(serializer, predicate, value);

        predicate.Property.SetValue(entity, listValue);

        return true;
    }

    public object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (!CanRead(reader))
            return null;

        var entity = CreateEntity(objectType, existingValue);

        var obj = serializer.Deserialize<JObject>(reader);

        foreach (var (propertyName, value) in obj)
        {
            if (value.Type is JTokenType.Null or JTokenType.Undefined or JTokenType.None)
                continue;

            if (TryReadUuid(propertyName, serializer, entity, value))
                continue;

            if (TryReadType(propertyName, serializer, entity, value))
                continue;

            if (!TryGetPredicate(objectType, propertyName, serializer, entity, value, out var predicate))
                continue;

            if (TryReadFloat32Vector(predicate, entity, value))
                continue;

            if (TryReadFacetedValues(predicate, entity, obj, value))
                continue;

            if (TryReadFacetPredicate(serializer, predicate, entity, value))
                continue;

            if (TryReadGeoObject(serializer, predicate, entity, value))
                continue;

            if (TryReadIEntity(serializer, predicate, entity, value))
                continue;

            if (TryReadIEntityEnumerable(serializer, predicate, entity, value))
                continue;

            using (getSetter(serializer))
                predicate.SetValue(entity, value.ToObject(predicate.Property.PropertyType));
        }

        return entity;
    }
}
