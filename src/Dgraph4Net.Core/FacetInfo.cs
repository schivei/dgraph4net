using System.Collections;
using System.Globalization;

namespace Dgraph4Net;

public readonly struct FacetInfo(string predicateName, string facetName) :
    ICloneable, IComparable, IComparable<string>, IConvertible,
    IEquatable<string>, IEnumerable<char>, IComparable<FacetInfo>,
    IEquatable<FacetInfo>
{
    public string PredicateName { get; } = predicateName;
    public string FacetName { get; } = facetName is ['@', ..] or ['|', ..] ? facetName[1..] : facetName;
    public bool IsI18n { get; } = facetName is ['@', ..];
    public char Separator { get; } = facetName is ['@', ..] ? '@' : '|';

    public static implicit operator FacetInfo((string predicateName, string facetName) tuple) =>
        new(tuple.predicateName, tuple.facetName);

    public static implicit operator (string predicateName, string facetName)(FacetInfo facet) =>
        (facet.PredicateName, facet.FacetName);

    public override string ToString() => $"{PredicateName}{Separator}{FacetName}";

    public static bool operator ==(FacetInfo left, FacetInfo right) =>
        left.PredicateName == right.PredicateName && left.FacetName == right.FacetName;

    public static bool operator !=(FacetInfo left, FacetInfo right) =>
        left.PredicateName != right.PredicateName || left.FacetName != right.FacetName;

    public override bool Equals(object? obj) =>
        obj is FacetInfo facet && this == facet;

    public override int GetHashCode() =>
        HashCode.Combine(PredicateName, FacetName);

    public object Clone()
    {
        FacetInfo clone = ToString();

        return clone;
    }

    public int CompareTo(object? obj)
    {
        if (obj is null)
            return 1;

        if (obj is FacetInfo facet)
            return CompareTo(facet.ToString());

        if (obj is string str)
            return CompareTo(str);

        return 0;
    }

    public int CompareTo(string? other)
    {
        if (other is null)
            return 1;

        return ToString().CompareTo(other);
    }

    public TypeCode GetTypeCode() => TypeCode.String;

    private IConvertible ConvertibleName => FacetName;

    bool IConvertible.ToBoolean(IFormatProvider? provider) =>
        ConvertibleName.ToBoolean(provider);

    byte IConvertible.ToByte(IFormatProvider? provider) =>
                ConvertibleName.ToByte(provider);

    char IConvertible.ToChar(IFormatProvider? provider) =>
        ConvertibleName.ToChar(provider);

    DateTime IConvertible.ToDateTime(IFormatProvider? provider) =>
        ConvertibleName.ToDateTime(provider);

    decimal IConvertible.ToDecimal(IFormatProvider? provider) =>
        ConvertibleName.ToDecimal(provider);

    double IConvertible.ToDouble(IFormatProvider? provider) =>
        ConvertibleName.ToDouble(provider);

    short IConvertible.ToInt16(IFormatProvider? provider) =>
        ConvertibleName.ToInt16(provider);

    int IConvertible.ToInt32(IFormatProvider? provider) =>
        ConvertibleName.ToInt32(provider);

    long IConvertible.ToInt64(IFormatProvider? provider) =>
        ConvertibleName.ToInt64(provider);

    sbyte IConvertible.ToSByte(IFormatProvider? provider) =>
        ConvertibleName.ToSByte(provider);

    float IConvertible.ToSingle(IFormatProvider? provider) =>
        ConvertibleName.ToSingle(provider);

    string IConvertible.ToString(IFormatProvider? provider) =>
        ConvertibleName.ToString(provider);

    object IConvertible.ToType(Type conversionType, IFormatProvider? provider) =>
        ConvertibleName.ToType(conversionType, provider);

    ushort IConvertible.ToUInt16(IFormatProvider? provider) =>
        ConvertibleName.ToUInt16(provider);

    uint IConvertible.ToUInt32(IFormatProvider? provider) =>
        ConvertibleName.ToUInt32(provider);

    ulong IConvertible.ToUInt64(IFormatProvider? provider) =>
        ConvertibleName.ToUInt64(provider);

    public bool Equals(string? other)
    {
        if (other is null)
            return false;

        return ToString().Equals(other);
    }

    public IEnumerator<char> GetEnumerator() =>
        ToString().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        ToString().GetEnumerator();

    public int CompareTo(FacetInfo other)
    {
        if (other == default)
            return 1;

        return ToString().CompareTo(other.ToString());
    }

    public bool Equals(FacetInfo other)
    {
        if (other == default)
            return false;

        return this == other;
    }

    public static FacetInfo operator +(FacetInfo left, FacetInfo right) =>
        new(left.PredicateName, right.FacetName);

    public static implicit operator FacetInfo(string[] facets)
    {
        if (facets.Length != 2)
            throw new ArgumentException("Array must have two elements", nameof(facets));

        return new(facets[0], facets[1]);
    }

    public static implicit operator string[](FacetInfo facet) =>
        [facet.PredicateName, facet.FacetName];

    public static implicit operator FacetInfo(string facet)
    {
        var names = facet.Split('|');

        if (names.Length == 2)
            return (names[0], names[1]);

        names = facet.Split('@');
        if (names.Length == 2)
            return (names[0], '@' + names[1]);

        throw new ArgumentException("Facet must have two elements", nameof(facet));
    }

    public static string ToJsonValue(object? value)
    {
        if (value is null)
            return "null";

        return value switch
        {
            bool b => b.ToString().ToLower(),
            DateTime dt => $"\"{dt:yyyy-MM-ddTHH:mm:ssZ}\"",
            DateTimeOffset dto => $"\"{dto:yyyy-MM-ddTHH:mm:ssZ}",
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            int i => i.ToString(),
            _ => $"\"{value}\""
        };
    }
}
