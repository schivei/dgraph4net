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
            if (reader.Value is null)
                return null;

            if (objectType == typeof(Uid) && reader.Value is Uid uid)
            {
                return uid;
            }
            else if (objectType == typeof(string) && reader.Value is string str)
            {
                return new Uid(str);
            }
            else if (objectType == typeof(int) ||
                objectType == typeof(uint) ||
                objectType == typeof(long) ||
                objectType == typeof(ulong) ||
                objectType == typeof(byte) ||
                objectType == typeof(char) ||
                objectType == typeof(sbyte) ||
                objectType == typeof(short) ||
                objectType == typeof(ushort))
            {
                return new Uid(Convert.ToUInt64(reader.Value), true);
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
}
