namespace Dgraph4Net;

public interface IFacetPredicate<T, TE> : IFacetPredicate where T : IEntity
{
    /// <summary>
    /// Gets or sets the value of the predicate.
    /// </summary>
    new TE PredicateValue { get; set; }

    /// <summary>
    /// Gets or sets the instance of the predicate.
    /// </summary>
    new T PredicateInstance { get; set; }

    /// <summary>
    /// Gets the value of the facet with the specified name.
    /// </summary>
    /// <typeparam name="TR"></typeparam>
    /// <param name="name"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    TR GetFacet<TR>(string name, TR defaultValue = default!)
        where TR : notnull, IComparable, IComparable<TR>, IEquatable<TR>;

    /// <summary>
    /// Sets the value of the facet with the specified name.
    /// </summary>
    /// <typeparam name="TR"></typeparam>
    /// <param name="facet"></param>
    /// <param name="value"></param>
    void SetFacet<TR>(string facet, TR? value)
        where TR : notnull, IComparable, IComparable<TR>, IEquatable<TR>;

    bool IsDummy();
}
