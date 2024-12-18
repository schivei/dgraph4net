namespace Dgraph4Net;

public interface IFacetedValue
{
    Type ValueType { get; }
    IDictionary<string, object?> Facets { get; }
    object? Value { get; set; }
    bool Drop { get; set; }

    /// <summary>
    /// Gets the value of the facet with the specified name.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    object? GetFacet(string name, object? defaultValue);

    /// <summary>
    /// Sets the value of the facet with the specified name.
    /// </summary>
    /// <param name="facet"></param>
    /// <param name="value"></param>
    void SetFacet(string facet, object? value);
}
