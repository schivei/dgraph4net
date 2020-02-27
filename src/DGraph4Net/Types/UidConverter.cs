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
            if (reader.Value is null || objectType != typeof(Uid))
                return existingValue;

            switch (reader.Value)
            {
                case Uid uid:
                    return uid;
                case string str:
                    return new Uid(str);
            }

            if (reader.Value is int ||
                reader.Value is uint ||
                reader.Value is long ||
                reader.Value is ulong ||
                reader.Value is byte ||
                reader.Value is char ||
                reader.Value is sbyte ||
                reader.Value is short ||
                reader.Value is ushort)
            {
                return new Uid(Convert.ToUInt64(reader.Value), true);
            }

            return existingValue;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            var uid = (value as Uid?)?.ToString();
            writer.WriteValue(uid?.StartsWith("0x") == true ? $"<{uid}>" : uid);
        }
    }
}
