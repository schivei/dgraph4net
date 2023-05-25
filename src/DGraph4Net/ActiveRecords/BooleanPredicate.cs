#nullable enable

using System;
using System.Collections.Generic;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct BooleanPredicate(IClassMap ClassMap, string PredicateName, bool Index = false, bool Upsert = false) : IPredicate
{
    public ISet<IFacet> Facets { get; } = new HashSet<IFacet>();

    readonly string IPredicate.ToSchemaPredicate() =>
        $"{PredicateName}: bool {(Index ? "@index(bool)" : "")} {(Upsert ? "@upsert" : "")} .";

    readonly string IPredicate.ToTypePredicate() =>
        PredicateName;

    public static BooleanPredicate operator |(BooleanPredicate lpa1, BooleanPredicate lpa2) =>
        lpa1.Merge(lpa2);

    public BooleanPredicate Merge(BooleanPredicate lpa) =>
        new(ClassMap, PredicateName, Index || lpa.Index, Upsert || lpa.Upsert);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            BooleanPredicate p => Merge(p),
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };
}
