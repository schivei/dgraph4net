using System.Collections.Concurrent;
using System.Reflection;

#nullable enable

namespace Dgraph4Net.ActiveRecords;

public abstract class ClassMap : IClassMap
{
    protected object _lock = new();

    public static ConcurrentDictionary<PropertyInfo, IPredicate> Predicates { get; } = new();

    public static ConcurrentDictionary<PropertyInfo, ConcurrentBag<IFacet>> Facets { get; } = new();

    public Type Type { get; protected internal set; }
    public string DgraphType { get; protected internal set; }

    /// <summary>
    /// Key is the property of the class, value is the property of the edge
    /// </summary>
    protected internal static ConcurrentDictionary<PropertyInfo, (PropertyInfo prop, IClassMap map)> PendingEdges { get; } = new();

    internal static IClassMap CreateInstance(Type type) =>
        (IClassMap)Activator.CreateInstance(type)!;

    public virtual void Start() { }
    public virtual void Finish() { }
}
