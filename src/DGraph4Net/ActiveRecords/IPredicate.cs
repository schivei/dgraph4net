#nullable enable

using System.Collections.Generic;
using System.Reflection;

namespace Dgraph4Net.ActiveRecords;

public interface IPredicate
{
    PropertyInfo Property { get; }
    IClassMap ClassMap { get; }
    string PredicateName { get; }
    internal string ToSchemaPredicate();
    internal string ToTypePredicate();
    IPredicate Merge(IPredicate p2);
    ISet<IFacet> Facets { get; }
    void SetValue(object? value, object? target);
}
