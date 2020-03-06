using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
                objectType == typeof(ulong) ||
                objectType == typeof(object);
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            var value = reader.Value;
            if (reader.Value is null || objectType != typeof(Uid))
            {
                var jo = JObject.Load(reader);
                if (!jo.TryGetValue("uid", out var tk) || tk is null)
                    return existingValue;
                value = tk.ToString();
            }

            switch (value)
            {
                case Uid uid:
                    return uid;
                case string str:
                    return new Uid(str);
            }

            if (value is int ||
                value is uint ||
                value is long ||
                value is ulong ||
                value is byte ||
                value is char ||
                value is sbyte ||
                value is short ||
                value is ushort)
            {
                return new Uid(Convert.ToUInt64(reader.Value), true);
            }

            return existingValue;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            var uid = (value as Uid?)?.ToString() ?? string.Empty;
            var isObj = writer.Path != "uid" && !writer.Path.EndsWith(".uid");

            if (string.IsNullOrEmpty(uid))
            {
                writer.WriteNull();
                return;
            }

            if (isObj)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("uid");
            }

            writer.WriteValue(uid);

            if (isObj)
            {
                writer.WriteEndObject();
            }
        }
    }
}
