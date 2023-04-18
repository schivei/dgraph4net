namespace Dgraph4Net.Core.GeoLocation.CoordinateReferenceSystem;

/// <summary>
/// Base Interface for CRSBase Object types.
/// </summary>
public interface ICRSObject
{
    /// <summary>
    /// Gets the CRS type.
    /// </summary>
    CRSType Type { get; }
}
