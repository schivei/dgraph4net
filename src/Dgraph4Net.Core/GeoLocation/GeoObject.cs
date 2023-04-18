using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Dgraph4Net.Core.GeoLocation.Converters;
using Dgraph4Net.Core.GeoLocation.CoordinateReferenceSystem;

namespace Dgraph4Net.Core.GeoLocation;

/// <summary>
///     Base class for all IGeometryObject implementing types
/// </summary>
public abstract class GeoObject : IGeoObject, IEqualityComparer<GeoObject>, IEquatable<GeoObject>
{
    internal static readonly DoubleTenDecimalPlaceComparer s_doubleComparer = new();

    /// <summary>
    ///     Gets or sets the (optional)
    ///     <see cref="https://tools.ietf.org/html/rfc7946#section-5">Bounding Boxes</see>.
    /// </summary>
    /// <value>
    ///     The value of <see cref="BoundingBoxes" /> must be a 2*n array where n is the number of dimensions represented in
    ///     the
    ///     contained geometries, with the lowest values for all axes followed by the highest values.
    ///     The axes order of a bbox follows the axes order of geometries.
    ///     In addition, the coordinate reference system for the bbox is assumed to match the coordinate reference
    ///     system of the GeoJSON object of which it is a member.
    /// </value>
    [JsonPropertyName("bbox")]
    public double[] BoundingBoxes { get; set; } = Array.Empty<double>();

    /// <summary>
    ///     Gets or sets the (optional)
    ///     <see cref="https://tools.ietf.org/html/rfc7946#section-4">
    ///         Coordinate Reference System
    ///         Object.
    ///     </see>
    /// </summary>
    /// <value>
    ///     The Coordinate Reference System Objects.
    /// </value>
    [JsonPropertyName("crs")]
    [JsonConverter(typeof(CrsConverter))]
    //[DefaultValue(typeof(DefaultCRS), "")]
    public ICRSObject CRS { get; set; }

    /// <summary>
    ///     The (mandatory) type of the
    ///     <see cref="https://tools.ietf.org/html/rfc7946#section-3">GeoJSON Object</see>.
    /// </summary>
    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public abstract GeoObjectType Type { get; }


    #region IEqualityComparer, IEquatable

    /// <summary>
    /// Determines whether the specified object is equal to the current object
    /// </summary>
    public override bool Equals(object obj)
    {
        return Equals(this, obj as GeoObject);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current object
    /// </summary>
    public bool Equals(GeoObject other)
    {
        return Equals(this, other);
    }

    /// <summary>
    /// Determines whether the specified object instances are considered equal
    /// </summary>
    public bool Equals(GeoObject left, GeoObject right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }
        if (right is null)
        {
            return false;
        }

        if (left.Type != right.Type)
        {
            return false;
        }

        if (!Equals(left.CRS, right.CRS))
        {
            return false;
        }

        var leftIsNull = left.BoundingBoxes is null;
        var rightIsNull = right.BoundingBoxes is null;
        var bothAreMissing = leftIsNull && rightIsNull;

        if (bothAreMissing || leftIsNull != rightIsNull)
        {
            return bothAreMissing;
        }

        return left.BoundingBoxes.SequenceEqual(right.BoundingBoxes, s_doubleComparer);
    }

    /// <summary>
    /// Determines whether the specified object instances are considered equal
    /// </summary>
    public static bool operator ==(GeoObject left, GeoObject right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }
        if (right is null)
        {
            return false;
        }
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether the specified object instances are not considered equal
    /// </summary>
    public static bool operator !=(GeoObject left, GeoObject right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Returns the hash code for this instance
    /// </summary>
    public override int GetHashCode()
    {
        return ((int)Type).GetHashCode();
    }

    /// <summary>
    /// Returns the hash code for the specified object
    /// </summary>
    public int GetHashCode(GeoObject obj)
    {
        return obj.GetHashCode();
    }

    #endregion
}
