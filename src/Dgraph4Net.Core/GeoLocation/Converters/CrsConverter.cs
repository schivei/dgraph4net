using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dgraph4Net.Core.GeoLocation.CoordinateReferenceSystem;

namespace Dgraph4Net.Core.GeoLocation.Converters;

/// <summary>
/// Converts <see cref="ICRSObject"/> types to and from JSON.
/// </summary>
public class CrsConverter : JsonConverter<ICRSObject>
{
    /// <summary>
    /// Determines whether this instance can convert the specified object type.
    /// </summary>
    /// <param name="objectType">Type of the object.</param>
    /// <returns>
    /// <c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.
    /// </returns>
    public override bool CanConvert(Type objectType)
    {
        return typeof(ICRSObject).IsAssignableFromType(objectType);
    }

    public override ICRSObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var loadType = JsonSerializer.Deserialize<LoadCRSObject>(ref reader, options);

        var properties = loadType["properties"]?.Deserialize<Dictionary<string, JsonElement>>(options) ?? new();

        switch (loadType.Type)
        {
            case CRSType.Unspecified:
                return new UnspecifiedCRS();
            case CRSType.Name when properties.TryGetValue("name", out var name):
                var named = new NamedCRS(name.GetString()!, loadType["properties"]?.Deserialize<Dictionary<string, object>>(options));
                return named;
            case CRSType.Link when properties.TryGetValue("href", out var link):
                var linked = new LinkedCRS(link.GetString()!, null, loadType["properties"]?.Deserialize<Dictionary<string, object>>(options));
                return linked;
            default:
                throw new NotSupportedException(string.Format("Type {0} unexpected.", loadType.Type));
        }
    }

    public override void Write(Utf8JsonWriter writer, ICRSObject value, JsonSerializerOptions options)
    {
        switch (value.Type)
        {
            case CRSType.Unspecified:
                writer.WriteNullValue();
                break;
            case CRSType.Name:
            case CRSType.Link:
                JsonSerializer.Serialize(writer, value);
                break;
            default:
                throw new SerializationException();
        }
    }
}
