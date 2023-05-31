using System.Text.Json.Serialization;

namespace Dgraph4Net.Core.GeoLocation;

internal sealed class GeometryObject : IGeometryObject
{
    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GeoObjectType Type { get; set; }
}
