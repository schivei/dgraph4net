using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dgraph4Net.Core.GeoLocation.Converters;

/// <summary>
/// Converter to read and write the <see cref="IEnumerable{MultiPolygon}" /> type.
/// </summary>
public class PolygonEnumerableConverter : JsonConverter<IEnumerable<Polygon>>
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
        return objectType == typeof(IEnumerable<Polygon>);
    }

    public override IEnumerable<Polygon> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var coordinates = JsonSerializer.Deserialize<IEnumerable<IEnumerable<IEnumerable<IEnumerable<double>>>>>(ref reader, options);
        return coordinates.Select(x => new Polygon(x));
    }

    public override void Write(Utf8JsonWriter writer, IEnumerable<Polygon> value, JsonSerializerOptions options)
    {
        var coordinates = value.Select(x => x.Coordinates.Select(x => x.Coordinates.ToCoordinates()));
        JsonSerializer.Serialize(writer, coordinates, options);
    }
}
