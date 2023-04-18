using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Dgraph4Net.Core.GeoLocation;

/// <summary>
/// Defines the FeatureCollection type.
/// </summary>
public class FeatureCollection : GeoObject, IEqualityComparer<FeatureCollection>, IEquatable<FeatureCollection>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureCollection" /> class.
    /// </summary>
    public FeatureCollection() : this(new List<Feature>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureCollection" /> class.
    /// </summary>
    /// <param name="features">The features.</param>
    public FeatureCollection(List<Feature> features)
    {
        Features = features ?? throw new ArgumentNullException(nameof(features));
    }

    public override GeoObjectType Type => GeoObjectType.FeatureCollection;

    /// <summary>
    /// Gets the features.
    /// </summary>
    /// <value>The features.</value>
    [JsonPropertyName("features")]
    [JsonRequired]
    public List<Feature> Features { get; private set; }

    #region IEqualityComparer, IEquatable

    /// <summary>
    /// Determines whether the specified object is equal to the current object
    /// </summary>
    public override bool Equals(object obj)
    {
        return Equals(this, obj as FeatureCollection);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current object
    /// </summary>
    public bool Equals(FeatureCollection other)
    {
        return Equals(this, other);
    }

    /// <summary>
    /// Determines whether the specified object instances are considered equal
    /// </summary>
    public bool Equals(FeatureCollection left, FeatureCollection right)
    {
        if (base.Equals(left, right))
        {
            return left.Features.SequenceEqual(right.Features);
        }

        return false;
    }

    /// <summary>
    /// Determines whether the specified object instances are considered equal
    /// </summary>
    public static bool operator ==(FeatureCollection left, FeatureCollection right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (right is null)
        {
            return false;
        }

        return left != null && left.Equals(right);
    }

    /// <summary>
    /// Determines whether the specified object instances are not considered equal
    /// </summary>
    public static bool operator !=(FeatureCollection left, FeatureCollection right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Returns the hash code for this instance
    /// </summary>
    public override int GetHashCode()
    {
        int hash = base.GetHashCode();
        foreach (var feature in Features)
        {
            hash = (hash * 397) ^ feature.GetHashCode();
        }

        return hash;
    }

    /// <summary>
    /// Returns the hash code for the specified object
    /// </summary>
    public int GetHashCode(FeatureCollection other)
    {
        return other.GetHashCode();
    }

    #endregion
}
