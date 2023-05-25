#nullable enable

using System;
using System.Collections.Generic;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct UidPredicate(IClassMap ClassMap) : IPredicate
{
    public string PredicateName => "uid";
    public ISet<IFacet> Facets { get; } = new HashSet<IFacet>();
    readonly string IPredicate.ToSchemaPredicate() => string.Empty;

    readonly string IPredicate.ToTypePredicate() => PredicateName;

    public static UidPredicate operator |(UidPredicate lpa1, UidPredicate lpa2) =>
        lpa1.Merge(lpa2);

    public UidPredicate Merge(UidPredicate _) =>
        new(ClassMap);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            UidPredicate p => Merge(p),
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };
}
