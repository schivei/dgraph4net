#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct UidPredicate(IClassMap ClassMap, PropertyInfo Property) : IPredicate
{
    public string PredicateName => "uid";
    public ISet<IFacet> Facets { get; } = new HashSet<IFacet>();
    readonly string IPredicate.ToSchemaPredicate() => string.Empty;

    readonly string IPredicate.ToTypePredicate() => PredicateName;

    public static UidPredicate operator |(UidPredicate lpa1, UidPredicate lpa2) =>
        lpa1.Merge(lpa2);

    public UidPredicate Merge(UidPredicate _) =>
        new(ClassMap, Property);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            UidPredicate p => Merge(p),
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };

    public void SetValue(object? value, object? target)
    {
        if (value is null)
            return;

        if (value is JsonElement element)
            value = element.GetString();

        Uid uid = value.ToString();

        Property.SetValue(target, uid);
    }
}
