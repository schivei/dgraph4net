using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dgraph4Net.Core.GeoLocation.Converters;

/// <summary>
/// Converter to read and write the <see cref="IEnumerable{LineString}" /> type.
/// </summary>
public class LineStringEnumerableConverter : JsonConverter<IEnumerable<LineString>>
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
        return typeof(IEnumerable<LineString>).IsAssignableFromType(objectType);
    }

    public override IEnumerable<LineString> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var rings = JsonSerializer.Deserialize<IEnumerable<IEnumerable<IEnumerable<double>>>>(ref reader, options);
        var lineStrings = new List<LineString>();
        foreach (var ring in rings)
        {
            var lineString = new LineString(ring.ToPositions());
            lineStrings.Add(lineString);
        }
        return lineStrings;
    }

    public override void Write(Utf8JsonWriter writer, IEnumerable<LineString> value, JsonSerializerOptions options)
    {
        var coordinates = value.Select(x => x.Coordinates.ToCoordinates());
        JsonSerializer.Serialize(writer, coordinates, options);
    }
}
