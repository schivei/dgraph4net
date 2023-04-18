using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dgraph4Net.Core.GeoLocation.Converters;

/// <summary>
/// Converter to read and write the <see cref="IEnumerable{Point}" /> type.
/// </summary>
public class PointEnumerableConverter : JsonConverter<IEnumerable<Point>>
{
    /// <inheritdoc />
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(IEnumerable<Point>);
    }

    public override IEnumerable<Point> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var coordinates = JsonSerializer.Deserialize<double[][]>(ref reader, options);
        return coordinates.ToPositions().Select(position => new Point(position));
    }

    public override void Write(Utf8JsonWriter writer, IEnumerable<Point> value, JsonSerializerOptions options)
    {
        var coordinates = value.Select(point => point.Coordinates.ToCoordinates()).ToArray();
        JsonSerializer.Serialize(writer, coordinates, options);
    }
}
