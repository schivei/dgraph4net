#nullable enable

using System;
using System.Collections.Generic;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct ListPredicate(IClassMap ClassMap, string PredicateName, string ListType, bool Count = true, bool Reversed = false) : IPredicate
{
    public ISet<IFacet> Facets { get; } = new HashSet<IFacet>();

    public ListPredicate Merge(ListPredicate lpa) =>
        new(ClassMap, PredicateName, ListType, Count || lpa.Count);

    readonly string IPredicate.ToSchemaPredicate() =>
        Reversed ? $"{PredicateName}: uid @reverse {(Count ? "@count" : "")} ." : $"{PredicateName}: [{ListType}] {(Count ? "@count" : "")} .";

    readonly string IPredicate.ToTypePredicate() =>
        Reversed ? $"<~{PredicateName}>" :
        PredicateName;

    public static ListPredicate operator |(ListPredicate lpa1, ListPredicate lpa2) =>
        lpa1.Merge(lpa2);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            ListPredicate p => Merge(p),
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };
}
