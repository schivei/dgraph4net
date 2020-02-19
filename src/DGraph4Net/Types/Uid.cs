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
            _uid = Clear($"0x{uid:N}".Substring(16));

        public Uid(ulong uid) =>
            _uid = Clear($"0x{uid:X}");

        private Uid(bool _) =>
            _uid = string.Empty;

        public static bool operator ==(Uid? uid, object? other) =>
            string.Equals(uid?.ToString(), other?.ToString());

        public static bool operator !=(Uid? uid, object? other) =>
            !string.Equals(uid?.ToString(), other?.ToString());

        public static implicit operator string(Uid uid) =>
            uid.ToString();

        public static implicit operator Uid(string uid) =>
            new Uid(uid);

        public static implicit operator Guid(Uid _) =>
            throw new InvalidCastException("Can't cast Uid to Guid because the Uid is too small.");

        public static implicit operator Uid(Guid uid) =>
            new Uid(uid);

        public static implicit operator ulong(Uid uid) =>
            ulong.Parse(uid.ToString().Substring(2), NumberStyles.HexNumber);

        public static implicit operator Uid(ulong uid) =>
            new Uid(uid);

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
            string.Compare(_uid, other.ToString());

        public int CompareTo(object obj) =>
            string.Compare(_uid, obj.ToString());

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
            var reg = new Regex("^(0x[a-fA-F0-9]{1,16}|_:[a-zA-Z0-9_]{1,32})$");
            if (!reg.IsMatch(uid) && @throw)
                throw new InvalidCastException($"Can't convert uid '{uid}' to Uid.");

            if (!reg.IsMatch(uid))
                return string.Empty;

            return uid.ToLowerInvariant();
        }

        public static Uid NewUid() =>
            Guid.NewGuid();

        public Uid Empty => new Uid(true);
    }
}
