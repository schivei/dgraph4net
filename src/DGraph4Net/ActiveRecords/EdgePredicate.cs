#nullable enable

using System;
using System.Collections.Generic;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct EdgePredicate<T>(IClassMap ClassMap, string PredicateName, bool Reverse, bool Count, bool Upsert) : IEdgePredicate
{
    public ISet<IFacet> Facets { get; } = new HashSet<IFacet>();

    readonly string IPredicate.ToSchemaPredicate() =>
        $"{PredicateName}: uid {(Count ? "@count" : "")} {(Reverse ? "@reverse" : "")} {(Upsert ? "@upsert" : "")} .";

    readonly string IPredicate.ToTypePredicate() =>
        PredicateName;

    public static EdgePredicate<T> operator |(EdgePredicate<T> lpa1, EdgePredicate<T> lpa2) =>
        lpa1.Merge(lpa2);

    public EdgePredicate<T> Merge(EdgePredicate<T> lpa) =>
        new(ClassMap, PredicateName, Reverse || lpa.Reverse, Count || lpa.Count, Upsert || lpa.Upsert);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            EdgePredicate<T> p => Merge(p),
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };
}
