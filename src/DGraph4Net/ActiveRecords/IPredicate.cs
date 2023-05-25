#nullable enable

using System.Collections.Generic;

namespace Dgraph4Net.ActiveRecords;

public interface IPredicate
{
    IClassMap ClassMap { get; }
    string PredicateName { get; }
    internal string ToSchemaPredicate();
    internal string ToTypePredicate();
    IPredicate Merge(IPredicate p2);

    ISet<IFacet> Facets { get; }
}
