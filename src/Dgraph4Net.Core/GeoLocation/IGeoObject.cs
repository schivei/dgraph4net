using Dgraph4Net.Core.GeoLocation.CoordinateReferenceSystem;

namespace Dgraph4Net.Core.GeoLocation;

/// <summary>
/// Base Interface for GeoObject types.
/// </summary>
public interface IGeoObject
{
    /// <summary>
    /// Gets the (mandatory) type of the Geo Object.
    /// </summary>
    /// <remarks>
    /// See https://tools.ietf.org/html/rfc7946#section-3
    /// </remarks>
    /// <value>
    /// The type of the object.
    /// </value>
    GeoObjectType Type { get; }

    /// <summary>
    /// Gets the (optional) Coordinate Reference System Object.
    /// </summary>
    /// <remarks>
    /// See https://tools.ietf.org/html/rfc7946#section-4
    /// </remarks>
    /// <value>
    /// The Coordinate Reference System Objects.
    /// </value>
    ICRSObject CRS { get; }

    /// <summary>
    /// Gets or sets the (optional) Bounding Boxes.
    /// </summary>
    /// <remarks>
    /// See https://tools.ietf.org/html/rfc7946#section-5
    /// </remarks>
    /// <value>
    /// The value of the bbox member must be a 2*n array where n is the number of dimensions represented in the
    /// contained geometries, with the lowest values for all axes followed by the highest values.
    /// The axes order of a bbox follows the axes order of geometries.
    /// In addition, the coordinate reference system for the bbox is assumed to match the coordinate reference
    /// system of the GeoJSON object of which it is a member.
    /// </value>
    double[] BoundingBoxes { get; set; }
}
