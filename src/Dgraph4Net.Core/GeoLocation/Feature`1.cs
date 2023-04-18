using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Dgraph4Net.Core.GeoLocation;

/// <summary>
/// Typed GeoJSON Feature class
/// </summary>
/// <remarks>Returns correctly typed Geometry property</remarks>
/// <typeparam name="TGeometry"></typeparam>
public class Feature<TGeometry> : Feature<TGeometry, IDictionary<string, object>>, IEquatable<Feature<TGeometry>> where TGeometry : IGeometryObject
{

    /// <summary>
    /// Initializes a new instance of the <see cref="Feature" /> class.
    /// </summary>
    /// <param name="geometry">The Geometry Object.</param>
    /// <param name="properties">The properties.</param>
    /// <param name="id">The (optional) identifier.</param>
    [JsonConstructor]
    public Feature(TGeometry geometry, IDictionary<string, object> properties = null, string id = null)
    : base(geometry, properties ?? new Dictionary<string, object>(), id)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Feature" /> class.
    /// </summary>
    /// <param name="geometry">The Geometry Object.</param>
    /// <param name="properties">
    /// Class used to fill feature properties. Any public member will be added to feature
    /// properties
    /// </param>
    /// <param name="id">The (optional) identifier.</param>
    public Feature(TGeometry geometry, object properties, string id = null)
    : this(geometry, GetDictionaryOfPublicProperties(properties), id)
    {
    }

    private static Dictionary<string, object> GetDictionaryOfPublicProperties(object properties)
    {
        if (properties == null)
        {
            return new Dictionary<string, object>();
        }

        return properties.GetType().GetTypeInfo().DeclaredProperties
            .Where(propertyInfo => propertyInfo.GetMethod.IsPublic)
            .ToDictionary(propertyInfo => propertyInfo.Name,
                propertyInfo => propertyInfo.GetValue(properties, null));
    }

    public bool Equals(Feature<TGeometry> other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;

        if (Geometry == null && other.Geometry == null)
        {
            return true;
        }

        if (Geometry == null && other.Geometry != null)
        {
            return false;
        }

        if (Geometry == null)
        {
            return false;
        }

        return EqualityComparer<TGeometry>.Default.Equals(Geometry, other.Geometry);
    }

    public override bool Equals(object obj)
    {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        return obj.GetType() == GetType() && Equals((Feature<TGeometry>)obj);
    }

    public override int GetHashCode()
    {
        return Geometry.GetHashCode();
    }

    public static bool operator ==(Feature<TGeometry> left, Feature<TGeometry> right)
    {
        return left?.Equals(right) ?? right is null;
    }

    public static bool operator !=(Feature<TGeometry> left, Feature<TGeometry> right)
    {
        return !(left?.Equals(right) ?? right is null);
    }
}
