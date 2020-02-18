using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

#nullable enable

// ReSharper disable once CheckNamespace
namespace System
{
    internal class UidConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Uid) ||
                objectType == typeof(string) ||
                objectType == typeof(ulong);
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (objectType == typeof(Uid) && reader.Value is Uid uid)
            {
                return uid;
            }
            else if (objectType == typeof(string) && reader.Value is string str)
            {
                return new Uid(str);
            }
            else if (objectType == typeof(ulong) && reader.Value is ulong ul)
            {
                return new Uid(ul);
            }
            else
            {
                return null;
            }
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            writer.WriteValue((value as Uid?)?.ToString());
        }
    }

    [JsonConverter(typeof(UidConverter))]
    public readonly struct Uid : IComparable, IComparable<Uid>, IEquatable<Uid>
    {
        private readonly string _uid;

        [JsonConstructor]
        public Uid(string uid) =>
            _uid = uid;

        public Uid(Guid uid) =>
            _uid = Clear($"{uid:N}".Substring(16));

        public Uid(ulong uid) =>
            _uid = Clear($"0x{uid:X}");

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

        public override string ToString() =>
            _uid;

        private static string Clear(string uid)
        {
            var reg = new Regex("^(0x)?([a-fA-F0-9]{1,16})$");
            if (!reg.IsMatch(uid))
                throw new InvalidCastException($"Can't convert uid '{uid}' to Uid.");

            if (!uid.EndsWith("0x"))
                uid = $"0x{uid}";

            return uid.ToLowerInvariant();
        }

        public static Uid NewUid() =>
            Guid.NewGuid();
    }
}
