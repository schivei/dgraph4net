using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;

namespace Dgraph4Net.Core.GeoLocation;

public static class GeoExtensions
{
    public static IPosition ToPosition(this IEnumerable<double> coordinates)
    {
        var coordinatesArray = coordinates.ToArray();
        return new Position(coordinatesArray[0], coordinatesArray[1], coordinatesArray.Length > 2 ? coordinatesArray[2] : null);
    }

    public static IEnumerable<double> ToCoordinates(this IPosition position)
    {
        var coordinates = new List<double>
        {
            position.Latitude,
            position.Longitude
        };

        if (position.Altitude.HasValue)
        {
            coordinates.Add(position.Altitude.Value);
        }
        return coordinates;
    }

    public static IEnumerable<IEnumerable<double>> ToCoordinates(this IEnumerable<IPosition> positions)
    {
        var coordinates = new List<IEnumerable<double>>();
        foreach (var position in positions)
        {
            coordinates.Add(ToCoordinates(position));
        }
        return coordinates;
    }

    public static IEnumerable<IPosition> ToPositions(this IEnumerable<double> coordinates, bool hasAltitudes = false)
    {
        var positions = new List<IPosition>();
        var coordinatesArray = coordinates.ToArray();

        hasAltitudes = hasAltitudes && coordinatesArray.Length % 3 == 0;

        for (int i = 0; i < coordinatesArray.Length; i += hasAltitudes ? 3 : 2)
        {
            positions.Add(new Position(coordinatesArray[i], coordinatesArray[i + 1], hasAltitudes ? coordinatesArray[i + 2] : null));
        }

        return positions;
    }

    public static IEnumerable<IPosition> ToPositions(this IEnumerable<IEnumerable<double>> coordinates)
    {
        var positions = new List<IPosition>();
        foreach (var coordinate in coordinates)
        {
            positions.AddRange(ToPositions(coordinate));
        }
        return positions;
    }

    public static IEnumerable<IEnumerable<double>> ToCoordinates(this IEnumerable<Position> positions)
    {
        var coordinates = new List<IEnumerable<double>>();
        foreach (var position in positions)
        {
            coordinates.Add(position.ToCoordinates());
        }
        return coordinates;
    }

    public static string ToGeoJson(this IGeoObject geoObject) =>
        JsonSerializer.Serialize(geoObject);

    public static object? ToGeoObject(this string geoJson, Type geoType) =>
        JsonSerializer.Deserialize(geoJson, geoType);
}
