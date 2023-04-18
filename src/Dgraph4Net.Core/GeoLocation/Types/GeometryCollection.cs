using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using Dgraph4Net.Core.GeoLocation.Converters;

namespace Dgraph4Net.Core.GeoLocation;

/// <summary>
/// Defines the GeometryCollection type.
/// </summary>
/// <remarks>
/// See https://tools.ietf.org/html/rfc7946#section-3.1.8
/// </remarks>
public class GeometryCollection : GeoObject, IGeometryObject, IEqualityComparer<GeometryCollection>, IEquatable<GeometryCollection>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeometryCollection" /> class.
    /// </summary>
    public GeometryCollection() : this(Array.Empty<IGeometryObject>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeometryCollection" /> class.
    /// </summary>
    /// <param name="geometries">The geometries contained in this GeometryCollection.</param>
    public GeometryCollection(IEnumerable<IGeometryObject> geometries)
    {
        Geometries = new ReadOnlyCollection<IGeometryObject>(
            geometries?.ToArray() ?? throw new ArgumentNullException(nameof(geometries)));
    }

    public override GeoObjectType Type => GeoObjectType.GeometryCollection;

    /// <summary>
    /// Gets the list of Polygons enclosed in this MultiPolygon.
    /// </summary>
    [JsonPropertyName("geometries")]
    [JsonRequired]
    [JsonConverter(typeof(GeometryConverter))]
    public ReadOnlyCollection<IGeometryObject> Geometries { get; private set; }

    #region IEqualityComparer, IEquatable

    /// <summary>
    /// Determines whether the specified object is equal to the current object
    /// </summary>
    public override bool Equals(object obj)
    {
        return Equals(this, obj as GeometryCollection);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current object
    /// </summary>
    public bool Equals(GeometryCollection other)
    {
        return Equals(this, other);
    }

    /// <summary>
    /// Determines whether the specified object instances are considered equal
    /// </summary>
    public bool Equals(GeometryCollection left, GeometryCollection right)
    {
        if (base.Equals(left, right))
        {
            return left.Geometries.SequenceEqual(right.Geometries);
        }
        return false;
    }

    /// <summary>
    /// Determines whether the specified object instances are considered equal
    /// </summary>
    public static bool operator ==(GeometryCollection left, GeometryCollection right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }
        if (right is null)
        {
            return false;
        }
        return left != null && left.Equals(right);
    }

    /// <summary>
    /// Determines whether the specified object instances are not considered equal
    /// </summary>
    public static bool operator !=(GeometryCollection left, GeometryCollection right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Returns the hash code for this instance
    /// </summary>
    public override int GetHashCode()
    {
        int hash = base.GetHashCode();
        foreach (var item in Geometries)
        {
            hash = (hash * 397) ^ item.GetHashCode();
        }
        return hash;
    }

    /// <summary>
    /// Returns the hash code for the specified object
    /// </summary>
    public int GetHashCode(GeometryCollection other)
    {
        return other.GetHashCode();
    }

    #endregion
}
