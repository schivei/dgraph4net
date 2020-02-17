using System.Globalization;
using System.Text.RegularExpressions;

#nullable enable

// ReSharper disable once CheckNamespace
namespace System
{
    public readonly struct Uid : IComparable, IComparable<Uid>, IEquatable<Uid>
    {
        private readonly string _uid;

        public Uid(string uid) =>
            _uid = Clear(uid);

        public Uid(Guid uid) =>
            _uid = Clear($"{uid:N}".Substring(16));

        public Uid(ulong uid) =>
            _uid = Clear($"0x{uid:X}");

        public static bool operator ==(Uid? uid, object? other) =>
            uid?.Equals(other) ?? false;

        public static bool operator !=(Uid? uid, object? other) =>
            !uid?.Equals(other) ?? true;

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

        public int CompareTo(Uid other) =>
            long.Parse(_uid.Substring(2), NumberStyles.HexNumber).CompareTo(other);

        public bool Equals(Uid other) =>
            CompareTo(other) == 0;

        public override bool Equals(object? obj) =>
            !(obj is null) && CompareTo(obj) == 0;

        public override int GetHashCode() =>
            _uid.GetHashCode();

        public override string ToString() =>
            _uid;

        public int CompareTo(object obj) =>
            obj switch
            {
                string str => CompareTo(new Uid(str)),
                Guid guid => CompareTo(new Uid(guid)),
                ulong ul => CompareTo(new Uid(ul)),
                Uid uid => CompareTo(uid),
                _ => throw new ArgumentException("The comparinson value is not a comparable type.", nameof(obj))
            };

        private static string Clear(string uid)
        {
            var reg = new Regex("^(0x)?([a-fA-F0-9]{1,16})$");
            if (!reg.IsMatch(uid))
                throw new InvalidCastException($"Can't convert uid '{uid}' to Uid.");

            if (!uid.EndsWith("0x"))
                uid = $"0x{uid}";

            return uid.ToLowerInvariant();
        }

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

        public static Uid NewUid() =>
            Guid.NewGuid();
    }
}
