using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Dgraph4Net;

namespace System;

public readonly partial struct Uid : IComparable, IComparable<Uid>, IEquatable<Uid>, IComparable<ulong>, IEquatable<ulong>, IEntityBase
{
    private readonly IDisposable? _unsubscriber;

    internal sealed class UidResolver : IObservable<Uid>
    {
        private readonly IList<IObserver<Uid>> _observers;

        public UidResolver()
        {
            _observers = [];
        }

        public IDisposable Subscribe(IObserver<Uid> observer)
        {
            if (!_observers.Contains(observer))
                _observers.Add(observer);

            return new Unsubscriber(this, observer);
        }

        public void Resolve(Uid source, Uid target)
        {
            _observers.OfType<UidObserver>().Where(o => o.Source == source)
                .ToList().ForEach(o => o.OnNext(target));
        }

        public void Unsubscribe(IObserver<Uid> observer)
        {
            _observers.Remove(observer);
        }
    }

    private sealed class UidObserver(Uid source) : IObserver<Uid>
    {
        public Uid Source { get; } = source;

        public void OnCompleted()
        {
            Source._unsubscriber?.Dispose();
        }

        public void OnError(Exception error)
        {
            throw error;
        }

        public void OnNext(Uid value)
        {
            Source._uid.Value = value._uid.Value;
        }
    }

    private sealed class Unsubscriber(UidResolver resolver, IObserver<Uid> observer) : IDisposable
    {
        private readonly UidResolver _resolver = resolver;
        private readonly IObserver<Uid> _observer = observer;

        public void Dispose()
        {
            _resolver.Unsubscribe(_observer);
        }
    }

    private sealed class UidValue(string value)
    {
        public string Value { get; set; } = value;
    }

    private readonly UidValue _uid = new(string.Empty);

    internal static readonly UidResolver s_resolver;

    static Uid()
    {
        s_resolver = new();
    }

    public readonly bool IsConcrete => !IsEmpty && _uid.Value.StartsWith("0x");

    public readonly bool IsReferenceOnly => !IsEmpty && _uid.Value.StartsWith("_:");

    public readonly bool IsEmpty => string.IsNullOrEmpty(_uid?.Value);

    public static Uid Empty { get; }

    public Uid()
    {
        _unsubscriber = null;
        _uid = new(string.Empty);

        if (IsReferenceOnly)
            _unsubscriber = s_resolver.Subscribe(new UidObserver(this));
    }

    [JsonConstructor]
    public Uid(string uid)
    {
        _unsubscriber = null;
        _uid = new(Clear(uid));
        if (IsReferenceOnly)
            _unsubscriber = s_resolver.Subscribe(new UidObserver(this));
    }

    public Uid(ulong uid, bool real)
    {
        _unsubscriber = null;
        var val = $"{(real ? "0x" : "_:")}{uid:X}";
        _uid = new(Clear(val));

        if (IsReferenceOnly)
            _unsubscriber = s_resolver.Subscribe(new UidObserver(this));
    }

    internal readonly void Resolve()
    {
        if (IsEmpty && _uid is not null)
            _uid.Value = NewUid()._uid.Value;
    }

    internal readonly void Replace(Uid uid)
    {
        if (_uid is not null)
            _uid.Value = uid._uid.Value;
    }

    public Uid(ulong uid) : this(uid, true) { }

    public static bool operator ==(Uid? uid, object? other) =>
        string.Equals(uid?.ToString(), other?.ToString());

    public static bool operator !=(Uid? uid, object? other) =>
        !string.Equals(uid?.ToString(), other?.ToString());

    public static implicit operator string(Uid uid) =>
        uid.ToString();

    public static implicit operator Uid(string uid) =>
        new(uid);

    public static implicit operator Uid(ulong uid) =>
        uid > 0 ? new Uid(uid) : throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

    public static explicit operator ulong(Uid uid) =>
        uid.IsEmpty || uid.IsReferenceOnly ? throw new InvalidCastException("Can't cast reference or empty Uid to ulong.") : ulong.Parse(uid.ToString(true), NumberStyles.HexNumber);

    public static implicit operator Uid(uint uid) =>
        uid > 0 ? new Uid(Convert.ToUInt64(uid)) : throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

    public static explicit operator uint(Uid uid) =>
        uid.IsEmpty || uid.IsReferenceOnly ? throw new InvalidCastException("Can't cast reference or empty Uid to uint.") : uint.Parse(uid.ToString(true), NumberStyles.HexNumber);

    public static implicit operator Uid(ushort uid) =>
        uid > 0 ? new Uid(Convert.ToUInt64(uid)) : throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

    public static explicit operator ushort(Uid uid) =>
        uid.IsEmpty || uid.IsReferenceOnly ? throw new InvalidCastException("Can't cast reference or empty Uid to ushort.") : ushort.Parse(uid.ToString(true), NumberStyles.HexNumber);

    public static implicit operator Uid(byte uid) =>
        uid > 0 ? new Uid(Convert.ToUInt64(uid)) : throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

    public static explicit operator byte(Uid uid) =>
        uid.IsEmpty || uid.IsReferenceOnly ? throw new InvalidCastException("Can't cast reference or empty Uid to byte.") : byte.Parse(uid.ToString(true), NumberStyles.HexNumber);

    public static implicit operator Uid(char uid) =>
        uid > 0 ? new Uid(Convert.ToUInt64(uid)) : throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

    public static explicit operator char(Uid uid) =>
        uid.IsEmpty || uid.IsReferenceOnly ? throw new InvalidCastException("Can't cast reference or empty Uid to char.") : (char)int.Parse(uid.ToString(true), NumberStyles.HexNumber);

    public static implicit operator Uid(long uid) =>
        uid > 0 ? new Uid(Convert.ToUInt64(uid)) : throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

    public static explicit operator long(Uid uid) =>
        uid.IsEmpty || uid.IsReferenceOnly ? throw new InvalidCastException("Can't cast reference or empty Uid to long.") : long.Parse(uid.ToString(true), NumberStyles.HexNumber);

    public static implicit operator Uid(int uid) =>
        uid > 0 ? new Uid(Convert.ToUInt64(uid)) : throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

    public static explicit operator int(Uid uid) =>
        uid.IsEmpty || uid.IsReferenceOnly ? throw new InvalidCastException("Can't cast reference or empty Uid to int.") : int.Parse(uid.ToString(true), NumberStyles.HexNumber);

    public static implicit operator Uid(short uid) =>
        uid > 0 ? new Uid(Convert.ToUInt64(uid)) : throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

    public static explicit operator short(Uid uid) =>
        uid.IsEmpty || uid.IsReferenceOnly ? throw new InvalidCastException("Can't cast reference or empty Uid to short.") : short.Parse(uid.ToString(true), NumberStyles.HexNumber);

    public static implicit operator Uid(sbyte uid) =>
        uid > 0 ? new Uid(Convert.ToUInt64(uid)) : throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

    public static explicit operator sbyte(Uid uid) =>
        uid.IsEmpty || uid.IsReferenceOnly ? throw new InvalidCastException("Can't cast reference or empty Uid to sbyte.") : sbyte.Parse(uid.ToString(true), NumberStyles.HexNumber);

    public static bool operator <(Uid left, Uid right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(Uid left, Uid right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(Uid left, Uid right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(Uid left, Uid right)
    {
        return left.CompareTo(right) >= 0;
    }

    /// <inheritdoc/>
    public readonly int CompareTo(Uid other) =>
        string.CompareOrdinal(_uid?.Value, other._uid?.Value);

    public readonly int CompareTo(object? obj) =>
        string.CompareOrdinal(_uid?.Value, obj?.ToString());

    public readonly bool Equals(Uid other) =>
        Equals(_uid?.Value, other._uid?.Value);

    public readonly override bool Equals(object? obj) =>
        Equals(_uid?.Value, obj?.ToString());

    public readonly override int GetHashCode() =>
        _uid?.GetHashCode() ?? int.MinValue;

    /// <inheritdoc/>
    public readonly override string ToString() =>
        ToString(false) ?? string.Empty;

    public readonly string ToString(bool dropHexPrefix) =>
        dropHexPrefix ? _uid?.Value[2..] : _uid?.Value;

    public static bool IsValid(string uid) =>
        IsValid(uid, out _);

    public static bool IsValid(string uid, out MatchCollection matches)
    {
        var reg = IsValidUid();

        matches = reg.Matches(uid);

        return reg.IsMatch(uid);
    }

    private static string Clear(string uid, bool @throw = true)
    {
        var isValid = IsValid(uid, out var matches);
        if (!isValid && @throw)
            throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

        if (!isValid)
            return string.Empty;

        return matches[0].Groups[2].Value.ToLowerInvariant();
    }

    public static Uid NewUid() => new($"_:{Guid.NewGuid():N}");

    [GeneratedRegex("^(<)?(0x[a-fA-F0-9]{1,16}|_:[a-zA-Z0-9_]{1,32})(>)?$")]
    private static partial Regex IsValidUid();

    public readonly bool Equals(ulong other) =>
        Equals(_uid.Value, $"0x{other:X}") ||
        Equals(_uid.Value, $"_:{other:X}");

    public readonly int CompareTo(ulong other) =>
        _uid.Value.StartsWith("0x") ?
            string.CompareOrdinal(_uid.Value, $"0x{other:X}") :
            string.CompareOrdinal(_uid.Value, $"_:{other:X}");
}
