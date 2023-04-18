using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dgraph4Net.Core.GeoLocation.CoordinateReferenceSystem;

internal sealed class LoadCRSObject : ICRSObject
{
    [JsonPropertyName("type")]
    public CRSType Type { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; }

    public JsonElement? this[string key] => ExtensionData.TryGetValue(key, out var value) ? value : null;
}
