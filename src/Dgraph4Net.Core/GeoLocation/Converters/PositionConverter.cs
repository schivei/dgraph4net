using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dgraph4Net.Core.GeoLocation.Converters;

/// <summary>
///     Converter to read and write an <see cref="IPosition" />, that is,
///     the coordinates of a <see cref="Point" />.
/// </summary>
public class PositionConverter : JsonConverter<Position>
{
    /// <summary>
    ///     Determines whether this instance can convert the specified object type.
    /// </summary>
    /// <param name="objectType">Type of the object.</param>
    /// <returns>
    ///     <c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.
    /// </returns>
    public override bool CanConvert(Type objectType)
    {
        return typeof(IPosition).IsAssignableFromType(objectType);
    }

    public override Position Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var coordinates = JsonSerializer.Deserialize<double[]>(ref reader, options);
        return (Position)coordinates.ToPosition();
    }

    public override void Write(Utf8JsonWriter writer, Position value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.ToCoordinates(), options);
    }
}
