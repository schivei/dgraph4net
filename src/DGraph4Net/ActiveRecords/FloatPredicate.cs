#nullable enable

using System;
using System.Collections.Generic;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct FloatPredicate(IClassMap ClassMap, string PredicateName, bool Index = false, bool Upsert = false) : IPredicate
{
    public ISet<IFacet> Facets { get; } = new HashSet<IFacet>();

    readonly string IPredicate.ToSchemaPredicate() =>
        $"{PredicateName}: float {(Index ? "@index(float)" : "")} {(Upsert ? "@upsert" : "")} .";
    readonly string IPredicate.ToTypePredicate() =>
        PredicateName;
    public static FloatPredicate operator |(FloatPredicate lpa1, FloatPredicate lpa2) =>
        lpa1.Merge(lpa2);
    public FloatPredicate Merge(FloatPredicate lpa) =>
        new(ClassMap, PredicateName, Index || lpa.Index, Upsert || lpa.Upsert);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            FloatPredicate p => Merge(p),
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };
}
