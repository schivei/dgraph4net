using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Dgraph4Net.Core.GeoLocation;

/// <summary>
/// A GeoJSON Feature Object.
/// </summary>
/// <remarks>
/// See https://tools.ietf.org/html/rfc7946#section-3.2
/// </remarks>
public class Feature : Feature<IGeometryObject>
{
    [JsonConstructor]
    public Feature(IGeometryObject geometry, IDictionary<string, object> properties = null, string id = null)
        : base(geometry, properties, id)
    {
    }

    public Feature(IGeometryObject geometry, object properties, string id = null)
        : base(geometry, properties, id)
    {
    }
}
