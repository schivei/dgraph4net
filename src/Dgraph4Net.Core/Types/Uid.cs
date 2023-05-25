using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Dgraph4Net;

using Google.Protobuf.Collections;

#nullable enable

namespace System;

[JsonConverter(typeof(UidConverter))]
public readonly partial struct Uid : IComparable, IComparable<Uid>, IEquatable<Uid>, IComparable<ulong>, IEquatable<ulong>, IEntityBase
{
    private readonly IDisposable? _unsubscriber;

    private sealed class UidResolver : IObservable<Uid>
    {
        private readonly IList<IObserver<Uid>> _observers;

        public UidResolver()
        {
            _observers = new List<IObserver<Uid>>();
        }

        public IDisposable Subscribe(IObserver<Uid> observer)
        {
            if (!_observers.Contains(observer))
                _observers.Add(observer);

            return new Unsubscriber(this, observer);
        }

        public void Resolve(MapField<string, string> uids)
        {
            uids.ToList().ForEach(kv =>
            {
                if (IsValid($"_:{kv.Key}") && IsValid(kv.Value))
                    Resolve($"_:{kv.Key}", kv.Value);
            });
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

    private sealed class UidObserver : IObserver<Uid>
    {
        public Uid Source { get; }

        public UidObserver(Uid source) => Source = source;

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

    private sealed class Unsubscriber : IDisposable
    {
        private readonly UidResolver _resolver;
        private readonly IObserver<Uid> _observer;

        public Unsubscriber(UidResolver resolver, IObserver<Uid> observer)
        {
            _resolver = resolver;
            _observer = observer;
        }

        public void Dispose()
        {
            _resolver.Unsubscribe(_observer);
        }
    }

    private sealed class UidValue
    {
        public UidValue(string value) => Value = value;
        public string Value { get; set; }
    }

    private readonly UidValue _uid;

    private static readonly UidResolver s_resolver;

    public static void Resolve(MapField<string, string> uids) =>
        s_resolver.Resolve(uids);

    static Uid()
    {
        s_resolver = new UidResolver();
    }

    public bool IsReferenceOnly => !IsEmpty && _uid.Value.StartsWith("_:");

    public bool IsEmpty => string.IsNullOrEmpty(_uid?.Value);

    [JsonConstructor]
    public Uid(string uid)
    {
        _unsubscriber = null;
        _uid = new UidValue(Clear(uid));
        if (IsReferenceOnly)
            _unsubscriber = s_resolver.Subscribe(new UidObserver(this));
    }

    public Uid(ulong uid, bool real)
    {
        _unsubscriber = null;
        var val = $"{(real ? "0x" : "_:")}{uid:X}";
        _uid = new UidValue(Clear(val));

        if (IsReferenceOnly)
            _unsubscriber = s_resolver.Subscribe(new UidObserver(this));
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

    public static implicit operator ulong(Uid uid) =>
        uid.IsEmpty || uid.IsReferenceOnly ? throw new InvalidCastException("Can't cast reference or empty Uid to ulong.") : ulong.Parse(uid.ToString(), NumberStyles.HexNumber);

    public static implicit operator Uid(uint uid) =>
        uid > 0 ? new Uid(Convert.ToUInt64(uid)) : throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

    public static implicit operator Uid(ushort uid) =>
        uid > 0 ? new Uid(Convert.ToUInt64(uid)) : throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

    public static implicit operator Uid(byte uid) =>
        uid > 0 ? new Uid(Convert.ToUInt64(uid)) : throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

    public static implicit operator Uid(char uid) =>
        uid > 0 ? new Uid(Convert.ToUInt64(uid)) : throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

    public static implicit operator Uid(long uid) =>
        uid > 0 ? new Uid(Convert.ToUInt64(uid)) : throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

    public static implicit operator Uid(int uid) =>
        uid > 0 ? new Uid(Convert.ToUInt64(uid)) : throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

    public static implicit operator Uid(short uid) =>
        uid > 0 ? new Uid(Convert.ToUInt64(uid)) : throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

    public static implicit operator Uid(sbyte uid) =>
        uid > 0 ? new Uid(Convert.ToUInt64(uid)) : throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

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
    public int CompareTo(Uid other) =>
        string.CompareOrdinal(_uid.Value, other.ToString());

    public int CompareTo(object? obj) =>
        string.CompareOrdinal(_uid.Value, obj?.ToString());

    public bool Equals(Uid other) =>
        Equals(_uid, other.ToString());

    public override bool Equals(object? obj) =>
        Equals(_uid, obj?.ToString());

    public override int GetHashCode() =>
        _uid?.GetHashCode() ?? int.MinValue;

    /// <inheritdoc/>
    public override string ToString() =>
        _uid?.Value ?? string.Empty;

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

    public static Uid NewUid() =>
        new(Convert.ToUInt64(RandomNumberGenerator.GetInt32(1, int.MaxValue)), false);

    [GeneratedRegex("^(<)?(0x[a-fA-F0-9]{1,16}|_:[a-zA-Z0-9_]{1,32})(>)?$")]
    private static partial Regex IsValidUid();

    public bool Equals(ulong other) =>
        Equals(_uid.Value, $"0x{other:X}") ||
        Equals(_uid.Value, $"_:{other:X}");

    public int CompareTo(ulong other) =>
        _uid.Value.StartsWith("0x") ?
            string.CompareOrdinal(_uid.Value, $"0x{other:X}") :
            string.CompareOrdinal(_uid.Value, $"_:{other:X}");
}
