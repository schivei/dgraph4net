using System;
using System.Collections.Concurrent;
using System.Reflection;

#nullable enable

namespace Dgraph4Net.ActiveRecords;

public interface IClassMap
{
    Type Type { get; }
    string DgraphType { get; }
    static ConcurrentDictionary<PropertyInfo, IPredicate> Predicates { get; }
    void Start();
    void Finish();
}
