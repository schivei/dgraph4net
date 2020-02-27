using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

#nullable enable

// ReSharper disable once CheckNamespace
namespace System
{
    [JsonConverter(typeof(UidConverter))]
    public readonly struct Uid : IComparable, IComparable<Uid>, IEquatable<Uid>
    {
        private readonly string _uid;

        public bool IsReferenceOnly => _uid.StartsWith("_:");

        public bool IsEmpty => string.IsNullOrEmpty(_uid);

        [JsonConstructor]
        public Uid(string uid) =>
            _uid = Clear(uid);

        public Uid(Guid uid) =>
            _uid = Clear($"_:{uid:N}".Substring(16));

        public Uid(ulong uid, bool real) =>
            _uid = Clear($"{(real ? "0x" : "_:")}{uid:X}");

        public Uid(ulong uid) : this(uid, false) { }

        public static bool operator ==(Uid? uid, object? other) =>
            string.Equals(uid?.ToString(), other?.ToString());

        public static bool operator !=(Uid? uid, object? other) =>
            !string.Equals(uid?.ToString(), other?.ToString());

        public static implicit operator string(Uid uid) =>
            uid.ToString();

        public static implicit operator Uid(string uid) =>
            new Uid(uid);

        public static implicit operator Uid(Guid uid) =>
            new Uid(uid);

        public static implicit operator Uid(ulong uid) =>
            new Uid(uid);

        public static implicit operator Uid(uint uid) =>
            new Uid(uid);

        public static implicit operator Uid(ushort uid) =>
            new Uid(Convert.ToUInt64(uid));

        public static implicit operator Uid(byte uid) =>
            new Uid(uid);

        public static implicit operator Uid(char uid) =>
            new Uid(uid);

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
            string.CompareOrdinal(_uid, other.ToString());

        public int CompareTo(object obj) =>
            string.CompareOrdinal(_uid, obj.ToString());

        public bool Equals(Uid other) =>
            string.Equals(_uid, other.ToString());

        public override bool Equals(object? obj) =>
            string.Equals(_uid, obj?.ToString());

        public override int GetHashCode() =>
            _uid.GetHashCode();

        /// <inheritdoc/>
        public override string ToString() =>
            _uid;

        private static string Clear(string uid, bool @throw = true)
        {
            var reg = new Regex("^((<)?(0x[a-fA-F0-9]{1,16})(>)?|_:[a-zA-Z0-9_]{1,32})$");
            if (!reg.IsMatch(uid) && @throw)
                throw new InvalidCastException($"Can't convert '{uid}' to Uid.");

            if (!reg.IsMatch(uid))
                return string.Empty;

            var matches = reg.Matches(uid);

            return matches[0].Groups[3].Value.ToLowerInvariant();
        }

        public static Uid NewUid() =>
            Guid.NewGuid();
    }
}
