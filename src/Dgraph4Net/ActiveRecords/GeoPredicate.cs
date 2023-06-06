using System.Reflection;
using NetGeo.Json;

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

        var geoExtensions = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch
                {
                    return Enumerable.Empty<Type>();
                }
            })
            .FirstOrDefault(t =>
            {
                try
                {
                    return t.Name == "GeoExtensions";
                }
                catch
                {
                    return false;
                }
            }) ?? throw new Exception("GeoExtensions not found. Please reference Dgraph4Net.Newtonsoft.Json or Dgraph4Net.System.Text.Json.");

        var geoObject = geoExtensions.GetMethod("ToGeoObject", BindingFlags.Public | BindingFlags.Static)
            ?.MakeGenericMethod(Property.PropertyType)
            ?.Invoke(null, new object[] { value }) as GeoObject;

        Property.SetValue(target, geoObject);
    }
}
