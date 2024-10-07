using System.Collections.Concurrent;
using System.Reflection;

namespace Dgraph4Net.ActiveRecords;

public interface IClassMap
{
    /// <summary>
    /// Gets the type of the class.
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// Gets the name of the class.
    /// </summary>
    string DgraphType { get; }

    /// <summary>
    /// Gets the pending edges.
    /// </summary>
    static ConcurrentDictionary<PropertyInfo, IPredicate> Predicates { get; }

    /// <summary>
    /// Starts the class map.
    /// </summary>
    void Start();

    /// <summary>
    /// Finishes the class map.
    /// </summary>
    void Finish();
}
