using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dgraph4Net.Core.GeoLocation.Converters;

/// <summary>
/// Converts <see cref="IGeoJSONObject"/> types to and from JSON.
/// </summary>
public class GeoJsonConverter : JsonConverter<IGeoObject>
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
        return typeof(IGeoObject).IsAssignableFromType(objectType);
    }

    /// <summary>
    /// Reads the geo json.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    /// <exception cref="System.Text.Json.Serialization.JsonReaderException">
    /// json must contain a "type" property
    /// or
    /// type must be a valid geojson object type
    /// </exception>
    /// <exception cref="System.NotSupportedException">
    /// Unknown geoJsonType {geoJsonType}
    /// </exception>
    internal static IGeoObject ReadGeoJson(ref Utf8JsonReader reader, JsonSerializerOptions options, GeoObjectType geoJsonType)
    {
        return geoJsonType switch
        {
            GeoObjectType.Point => JsonSerializer.Deserialize<Point>(ref reader, options),
            GeoObjectType.MultiPoint => JsonSerializer.Deserialize<MultiPoint>(ref reader, options),
            GeoObjectType.LineString => JsonSerializer.Deserialize<LineString>(ref reader, options),
            GeoObjectType.MultiLineString => JsonSerializer.Deserialize<MultiLineString>(ref reader, options),
            GeoObjectType.Polygon => JsonSerializer.Deserialize<Polygon>(ref reader, options),
            GeoObjectType.MultiPolygon => JsonSerializer.Deserialize<MultiPolygon>(ref reader, options),
            GeoObjectType.GeometryCollection => JsonSerializer.Deserialize<GeometryCollection>(ref reader, options),
            GeoObjectType.Feature => JsonSerializer.Deserialize<Feature>(ref reader, options),
            GeoObjectType.FeatureCollection => JsonSerializer.Deserialize<FeatureCollection>(ref reader, options),
            _ => throw new NotSupportedException($"Unknown geoJsonType {geoJsonType}"),
        };
    }

    internal static object WriteGeoJson(IGeoObject value, GeoObjectType geoObjectType)
    {
        return value switch
        {
            Point point => point.Coordinates.ToCoordinates(),
            MultiPoint multiPoint => multiPoint.Coordinates.Select(c => c.Coordinates.ToCoordinates()),
            LineString lineString => lineString.Coordinates.ToCoordinates(),
            MultiLineString multiLineString => multiLineString.Coordinates.Select(c => c.Coordinates.ToCoordinates()),
            Polygon polygon => polygon.Coordinates.Select(c => c.Coordinates.ToCoordinates()),
            MultiPolygon multiPolygon => multiPolygon.Coordinates.Select(c => c.Coordinates.Select(c => c.Coordinates.ToCoordinates())),
            GeometryCollection geometryCollection => geometryCollection.Geometries.OfType<IGeoObject>().Select(g => WriteGeoJson(g, g.Type)),
            _ => default!,
        };
    }

    public override IGeoObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                var g = JsonSerializer.Deserialize<GeometryObject>(ref reader, options);
                return ReadGeoJson(ref reader, options, g.Type);
            case JsonTokenType.StartArray:
                var values = JsonSerializer.Deserialize<List<GeometryObject>>(ref reader, options);
                var geometries = new List<IGeometryObject>(values.Count);
                foreach (var value in values)
                {
                    geometries.Add((IGeometryObject)ReadGeoJson(ref reader, options, value.Type));
                }
                return new GeometryCollection(geometries);
        }

        throw new Exception("expected null, object or array token but received " + reader.TokenType);
    }

    public override void Write(Utf8JsonWriter writer, IGeoObject value, JsonSerializerOptions options)
    {
        var g = (IGeometryObject)value;

        if (g.Type == GeoObjectType.Feature || g.Type == GeoObjectType.FeatureCollection)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
        else
        {
            JsonSerializer.Serialize(writer, WriteGeoJson(value, g.Type), options);
        }
    }
}
