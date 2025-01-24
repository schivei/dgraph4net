using System.Linq.Expressions;

namespace Dgraph4Net;

public interface IEntity : IEntityBase
{
    /// <summary>
    /// Gets or sets the value of the facet with the specified name.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    object? this[FacetInfo index] { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the entity.
    /// </summary>
    Uid Uid { get; }

    /// <summary>
    /// Gets or sets the type of the entity.
    /// </summary>
    string[] DgraphType { get; set; }

    /// <summary>
    /// Gets the facets of the entity.
    /// </summary>
    IDictionary<FacetInfo, object?> Facets { get; }

    /// <summary>
    /// Sets the value of the facet with the specified name.
    /// </summary>
    /// <param name="facet"></param>
    /// <param name="value"></param>
    void SetFacet(FacetInfo facet, object? value);

    /// <summary>
    /// Sets the value of the facet with the specified name.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="value"></param>
    void SetFacet<T>(Expression<Func<T, object?>> predicate, string name, object? value) where T : IEntity;

    /// <summary>
    /// Removes the facet with the specified name.
    /// </summary>
    /// <param name="facet"></param>
    void RemoveFacet(FacetInfo facet);

    /// <summary>
    /// Gets the value of the facet with the specified name.
    /// </summary>
    /// <typeparam name="T">IEntity</typeparam>
    /// <typeparam name="TE">Predicate value type</typeparam>
    /// <typeparam name="TR">The type of a facet</typeparam>
    /// <param name="name">The name of facet, preced it with '@' if you whant to get a i18n facet value</param>
    /// <param name="from">The predicate where facet are attached</param>
    /// <param name="defaultValue">Default value if facet not found</param>
    /// <returns>The facet value</returns>
    TR GetFacet<T, TE, TR>(string name, Expression<Func<T, TE?>> from, TR defaultValue = default!)
        where T : IEntity
        where TR : notnull, IComparable, IComparable<TR>, IEquatable<TR>;
}
