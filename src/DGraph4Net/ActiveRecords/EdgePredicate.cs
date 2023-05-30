#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct EdgePredicate<T>(IClassMap ClassMap, PropertyInfo Property, string PredicateName, bool Reverse, bool Count) : IEdgePredicate
{
    public ISet<IFacet> Facets { get; } = new HashSet<IFacet>();

    readonly string IPredicate.ToSchemaPredicate() =>
        $"{PredicateName}: uid {(Count ? "@count" : "")} {(Reverse ? "@reverse" : "")} .";

    readonly string IPredicate.ToTypePredicate() =>
        PredicateName;

    public static EdgePredicate<T> operator |(EdgePredicate<T> lpa1, EdgePredicate<T> lpa2) =>
        lpa1.Merge(lpa2);

    public EdgePredicate<T> Merge(EdgePredicate<T> lpa) =>
        new(ClassMap, Property, PredicateName, Reverse || lpa.Reverse, Count || lpa.Count);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            EdgePredicate<T> p => Merge(p),
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

        Property.SetValue(target, Convert.ChangeType(value, Property.PropertyType));
    }
}
