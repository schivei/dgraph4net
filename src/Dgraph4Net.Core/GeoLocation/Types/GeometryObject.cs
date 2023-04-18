using System.Text.Json.Serialization;

namespace Dgraph4Net.Core.GeoLocation;

internal sealed class GeometryObject : IGeometryObject
{
    [JsonPropertyName("type")]
    public GeoObjectType Type { get; set; }
}
