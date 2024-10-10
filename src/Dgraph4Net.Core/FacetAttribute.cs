using System.Reflection;

namespace Dgraph4Net;

/// <summary>
/// Represents a facet property.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="name"></param>
/// <param name="propertyName"></param>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public abstract class FacetAttribute(string name, PropertyInfo propertyInfo) : Attribute
{
    /// <summary>
    /// The property of the entity.
    /// </summary>
    public PropertyInfo Property { get; } = propertyInfo ?? throw new ArgumentException($"Property not found in {name}");

    /// <summary>
    /// The name of the facet.
    /// </summary>
    public string Name { get; } = name is ['@', ..] ? name.AsSpan(1).ToString() : name;

    /// <summary>
    /// Indicates if the facet is internationalized.
    /// </summary>
    public bool IsI18n { get; } = name is ['@', ..];
}
