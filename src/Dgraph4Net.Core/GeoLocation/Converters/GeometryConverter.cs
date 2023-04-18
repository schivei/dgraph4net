using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dgraph4Net.Core.GeoLocation.Converters;

/// <summary>
/// Converts <see cref="IGeometryObject"/> types to and from JSON.
/// </summary>
public class GeometryConverter : JsonConverter<IGeometryObject>
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
        return typeof(IGeometryObject).IsAssignableFromType(objectType);
    }

    public override IGeometryObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                var g = JsonSerializer.Deserialize<GeometryObject>(ref reader, options);
                return (IGeometryObject)GeoJsonConverter.ReadGeoJson(ref reader, options, g.Type);
            case JsonTokenType.StartArray:
                var values = JsonSerializer.Deserialize<List<GeometryObject>>(ref reader, options);
                var geometries = new List<IGeometryObject>(values.Count);
                foreach (var value in values)
                {
                    geometries.Add((IGeometryObject)GeoJsonConverter.ReadGeoJson(ref reader, options, value.Type));
                }
                return new GeometryCollection(geometries);
        }

        throw new Exception("expected null, object or array token but received " + reader.TokenType);
    }

    public override void Write(Utf8JsonWriter writer, IGeometryObject value, JsonSerializerOptions options)
    {
        if (value.Type == GeoObjectType.Feature || value.Type == GeoObjectType.FeatureCollection)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
        else
        {
            JsonSerializer.Serialize(writer, GeoJsonConverter.WriteGeoJson((IGeoObject)value, value.Type), options);
        }
    }
}
