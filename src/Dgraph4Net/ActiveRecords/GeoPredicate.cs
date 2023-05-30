#nullable enable

using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Dgraph4Net.Core.GeoLocation;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct GeoPredicate(IClassMap ClassMap, PropertyInfo Property, string PredicateName, bool Index = false, bool Upsert = false) : IPredicate
{
    public ISet<IFacet> Facets { get; } = new HashSet<IFacet>();

    readonly string IPredicate.ToSchemaPredicate() =>
        $"{PredicateName}: geo {(Index || Upsert ? "@index(geo)" : "")} {(Upsert ? "@upsert" : "")} .";

    readonly string IPredicate.ToTypePredicate() =>
        PredicateName;

    public static GeoPredicate operator |(GeoPredicate lpa1, GeoPredicate lpa2) =>
        lpa1.Merge(lpa2);

    public GeoPredicate Merge(GeoPredicate lpa) =>
        new(ClassMap, Property, PredicateName, Index || lpa.Index, Upsert || lpa.Upsert);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            GeoPredicate p => Merge(p),
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };

    public void SetValue(object? value, object? target)
    {
        if (value is null)
            return;

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                value = element.GetString();
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                Property.SetValue(target, element.Deserialize(Property.PropertyType));
                return;
            }
        }

        var geoObject = value.ToString().ToGeoObject(Property.PropertyType);

        Property.SetValue(target, geoObject);
    }
}
