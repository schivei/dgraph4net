#nullable enable

using System;
using System.Collections.Generic;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct IntegerPredicate(IClassMap ClassMap, string PredicateName, bool Index = false, bool Upsert = false) : IPredicate
{
    public ISet<IFacet> Facets { get; } = new HashSet<IFacet>();

    readonly string IPredicate.ToSchemaPredicate() =>
        $"{PredicateName}: int {(Index ? "@index(int)" : "")} {(Upsert ? "@upsert" : "")} .";
    readonly string IPredicate.ToTypePredicate() =>
        PredicateName;

    public static IntegerPredicate operator |(IntegerPredicate lpa1, IntegerPredicate lpa2) =>
        lpa1.Merge(lpa2);

    public IntegerPredicate Merge(IntegerPredicate lpa) =>
        new(ClassMap, PredicateName, Index || lpa.Index, Upsert || lpa.Upsert);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            IntegerPredicate p => Merge(p),
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };
}
