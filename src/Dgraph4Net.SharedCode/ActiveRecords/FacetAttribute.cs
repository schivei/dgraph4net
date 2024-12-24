namespace Dgraph4Net.ActiveRecords;

/// <summary>
/// Represents a facet property.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="name"></param>
/// <param name="propertyName"></param>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class FacetAttribute<T>(string name, string propertyName) : FacetAttribute(name, typeof(T).GetProperty(propertyName)) where T : AEntity<T>
{
}
