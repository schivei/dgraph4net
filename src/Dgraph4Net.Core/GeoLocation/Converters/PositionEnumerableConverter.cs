using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dgraph4Net.Core.GeoLocation.Converters;

/// <summary>
/// Converter to read and write the <see cref="IEnumerable{IPosition}" /> type.
/// </summary>
public class PositionEnumerableConverter : JsonConverter<IEnumerable<IPosition>>
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
        return typeof(IEnumerable<IPosition>).IsAssignableFromType(objectType);
    }

    public override IEnumerable<IPosition> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var coordinates = JsonSerializer.Deserialize<double[][]>(ref reader, options);
        return coordinates.ToPositions();
    }

    public override void Write(Utf8JsonWriter writer, IEnumerable<IPosition> value, JsonSerializerOptions options)
    {
        var coordinates = value.ToCoordinates();
        JsonSerializer.Serialize(writer, coordinates, options);
    }
}
