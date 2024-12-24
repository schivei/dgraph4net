namespace Dgraph4Net;

public interface IFacetedValue<T> : IFacetedValue
{
    new T? Value { get; set; }

    /// <summary>
    /// Gets the value of the facet with the specified name.
    /// </summary>
    /// <typeparam name="TR"></typeparam>
    /// <param name="name"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    TR? GetFacet<TR>(string name, TR defaultValue = default!)
        where TR : notnull, IComparable, IComparable<TR>, IEquatable<TR>;

    /// <summary>
    /// Sets the value of the facet with the specified name.
    /// </summary>
    /// <typeparam name="TR"></typeparam>
    /// <param name="facet"></param>
    /// <param name="value"></param>
    void SetFacet<TR>(string facet, TR? value)
        where TR : notnull, IComparable, IComparable<TR>, IEquatable<TR>;
}
