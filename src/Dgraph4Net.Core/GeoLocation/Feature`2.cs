using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Dgraph4Net.Core.GeoLocation.Converters;

namespace Dgraph4Net.Core.GeoLocation;

/// <summary>
/// A GeoJSON Feature Object; generic version for strongly typed <see cref="Geometry"/>
/// and <see cref="Properties"/>
/// </summary>
/// <remarks>
/// See https://tools.ietf.org/html/rfc7946#section-3.2
/// </remarks>
public class Feature<TGeometry, TProps> : GeoObject, IEquatable<Feature<TGeometry, TProps>>
    where TGeometry : IGeometryObject
{
    [JsonConstructor]
    public Feature(TGeometry geometry, TProps properties, string id = null)
    {
        Geometry = geometry;
        Properties = properties;
        Id = id;
    }

    public override GeoObjectType Type => GeoObjectType.Feature;

    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("geometry")]
    [JsonConverter(typeof(GeometryConverter))]
    public TGeometry Geometry { get; }

    [JsonPropertyName("properties")]
    public TProps Properties { get; }

    /// <summary>
    /// Equality comparer.
    /// </summary>
    /// <remarks>
    /// In contrast to <see cref="Feature.Equals(Feature)"/>, this implementation returns true only
    /// if <see cref="Id"/> and <see cref="Properties"/> are also equal. See
    /// <a href="https://github.com/GeoJSON-Net/GeoJSON.Net/issues/80">#80</a> for discussion. The rationale
    /// here is that a user explicitly specifying the property type most probably cares about the properties
    /// equality.
    /// </remarks>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool Equals(Feature<TGeometry, TProps> other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return base.Equals(other)
               && string.Equals(Id, other.Id)
               && EqualityComparer<TGeometry>.Default.Equals(Geometry, other.Geometry)
               && EqualityComparer<TProps>.Default.Equals(Properties, other.Properties);
    }

    public override bool Equals(object obj)
    {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != GetType())
            return false;
        return Equals((Feature<TGeometry, TProps>)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Id, Geometry, Properties);
    }

    public static bool operator ==(Feature<TGeometry, TProps> left, Feature<TGeometry, TProps> right)
    {
        return object.Equals(left, right);
    }

    public static bool operator !=(Feature<TGeometry, TProps> left, Feature<TGeometry, TProps> right)
    {
        return !object.Equals(left, right);
    }
}
