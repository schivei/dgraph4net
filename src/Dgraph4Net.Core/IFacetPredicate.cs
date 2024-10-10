using System.Reflection;

namespace Dgraph4Net;

public interface IFacetPredicate
{
    /// <summary>
    /// Gets or sets the value of the facet.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    object? this[FacetInfo index] { get; set; }

    /// <summary>
    /// Gets or sets the predicate.
    /// </summary>
    PropertyInfo PredicateProperty { get; set; }

    /// <summary>
    /// Gets or sets the instance of the predicate.
    /// </summary>
    object? PredicateInstance { get; set; }

    /// <summary>
    /// Gets or sets the value of the predicate.
    /// </summary>
    object? PredicateValue { get; set; }

    /// <summary>
    /// Gets the facets of the predicate.
    /// </summary>
    public IDictionary<FacetInfo, object?> Facets { get; }

    /// <summary>
    /// Sets the value of the facet with the specified name.
    /// </summary>
    /// <param name="facet"></param>
    /// <param name="value"></param>
    void SetFacet(FacetInfo facet, object? value);

    /// <summary>
    /// Removes the facet with the specified name.
    /// </summary>
    /// <param name="facet"></param>
    void RemoveFacet(FacetInfo facet);
}
