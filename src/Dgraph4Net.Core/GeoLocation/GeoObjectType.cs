namespace Dgraph4Net.Core.GeoLocation;

/// <summary>
/// Defines the Geo Objects types.
/// </summary>
public enum GeoObjectType
{
    /// <summary>
    /// Defines the Point type.
    /// </summary>
    /// <remarks>
    /// See https://tools.ietf.org/html/rfc7946#section-3.1.2
    /// </remarks>
    Point,

    /// <summary>
    /// Defines the MultiPoint type.
    /// </summary>
    /// <remarks>
    /// See https://tools.ietf.org/html/rfc7946#section-3.1.3
    /// </remarks>
    MultiPoint,

    /// <summary>
    /// Defines the LineString type.
    /// </summary>
    /// <remarks>
    /// See https://tools.ietf.org/html/rfc7946#section-3.1.4
    /// </remarks>
    LineString,

    /// <summary>
    /// Defines the MultiLineString type.
    /// </summary>
    /// <remarks>
    /// See https://tools.ietf.org/html/rfc7946#section-3.1.5
    /// </remarks>
    MultiLineString,

    /// <summary>
    /// Defines the Polygon type.
    /// </summary>
    /// <remarks>
    /// See https://tools.ietf.org/html/rfc7946#section-3.1.6
    /// </remarks> 
    Polygon,

    /// <summary>
    /// Defines the MultiPolygon type.
    /// </summary>
    /// <remarks>
    /// See https://tools.ietf.org/html/rfc7946#section-3.1.7
    /// </remarks>
    MultiPolygon,

    /// <summary>
    /// Defines the GeometryCollection type.
    /// </summary>
    /// <remarks>
    /// See https://tools.ietf.org/html/rfc7946#section-3.1.8
    /// </remarks>
    GeometryCollection,

    /// <summary>
    /// Defines the Feature type.
    /// </summary>
    /// <remarks>
    /// See https://tools.ietf.org/html/rfc7946#section-3.2
    /// </remarks>
    Feature,

    /// <summary>
    /// Defines the FeatureCollection type.
    /// </summary>
    /// <remarks>
    /// See https://tools.ietf.org/html/rfc7946#section-3.3
    /// </remarks>
    FeatureCollection
}
