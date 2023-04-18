using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using Dgraph4Net.Core.GeoLocation.Converters;

namespace Dgraph4Net.Core.GeoLocation;

/// <summary>
/// Defines the MultiPolygon type.
/// </summary>
/// <remarks>
/// See https://tools.ietf.org/html/rfc7946#section-3.1.7
/// </remarks>
public class MultiPolygon : GeoObject, IGeometryObject, IEqualityComparer<MultiPolygon>, IEquatable<MultiPolygon>
{

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiPolygon" /> class.
    /// </summary>
    /// <param name="polygons">The polygons contained in this MultiPolygon.</param>
    public MultiPolygon(IEnumerable<Polygon> polygons)
    {
        Coordinates = new ReadOnlyCollection<Polygon>(
            polygons?.ToArray() ?? throw new ArgumentNullException(nameof(polygons)));
    }

    /// <summary>
    /// Initializes a new <see cref="MultiPolygon" /> from a 4-d array of <see cref="double" />s
    /// that matches the "coordinates" field in the JSON representation.
    /// </summary>
    [JsonConstructor]
    public MultiPolygon(IEnumerable<IEnumerable<IEnumerable<IEnumerable<double>>>> coordinates)
        : this(coordinates?.Select(polygon => new Polygon(polygon))
               ?? throw new ArgumentNullException(nameof(coordinates)))
    {
    }

    public override GeoObjectType Type => GeoObjectType.MultiPolygon;

    /// <summary>
    /// The list of Polygons enclosed in this <see cref="MultiPolygon"/>.
    /// </summary>
    [JsonPropertyName("coordinates")]
    [JsonRequired]
    [JsonConverter(typeof(PolygonEnumerableConverter))]
    public ReadOnlyCollection<Polygon> Coordinates { get; }

    #region IEqualityComparer, IEquatable

    /// <summary>
    /// Determines whether the specified object is equal to the current object
    /// </summary>
    public override bool Equals(object obj)
    {
        return Equals(this, obj as MultiPolygon);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current object
    /// </summary>
    public bool Equals(MultiPolygon other)
    {
        return Equals(this, other);
    }

    /// <summary>
    /// Determines whether the specified object instances are considered equal
    /// </summary>
    public bool Equals(MultiPolygon left, MultiPolygon right)
    {
        if (base.Equals(left, right))
        {
            return left.Coordinates.SequenceEqual(right.Coordinates);
        }
        return false;
    }

    /// <summary>
    /// Determines whether the specified object instances are considered equal
    /// </summary>
    public static bool operator ==(MultiPolygon left, MultiPolygon right)
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
    public static bool operator !=(MultiPolygon left, MultiPolygon right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Returns the hash code for this instance
    /// </summary>
    public override int GetHashCode()
    {
        int hash = base.GetHashCode();
        foreach (var item in Coordinates)
        {
            hash = (hash * 397) ^ item.GetHashCode();
        }
        return hash;
    }

    /// <summary>
    /// Returns the hash code for the specified object
    /// </summary>
    public int GetHashCode(MultiPolygon other)
    {
        return other.GetHashCode();
    }

    #endregion
}