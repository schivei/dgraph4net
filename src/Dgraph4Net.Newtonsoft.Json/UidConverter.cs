using Newtonsoft.Json;

namespace Dgraph4Net.Newtonsoft.Json;

internal class UidConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(Uid) ||
               objectType == typeof(string) ||
               objectType == typeof(int) ||
               objectType == typeof(uint) ||
               objectType == typeof(long) ||
               objectType == typeof(ulong) ||
               objectType == typeof(byte) ||
               objectType == typeof(char) ||
               objectType == typeof(sbyte) ||
               objectType == typeof(short) ||
               objectType == typeof(ushort);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var value = reader.Value;

        if (value is null)
            return existingValue;

        if (objectType != typeof(Uid))
        {
            return JsonSerializer.Create(new JsonSerializerSettings())
                .Deserialize(reader, objectType);
        }

        if (string.IsNullOrEmpty(value?.ToString()?.Trim()))
        {
            return default(Uid);
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
        if (value is not Uid uid)
        {
            if (value is null)
            {
                writer.WriteNull();
            }
            else
            {
                var settings = new JsonSerializerSettings();
                settings.Converters.Clear();

                JsonSerializer.Create(settings).Serialize(writer, value);
            }

            return;
        }

        if (uid.IsEmpty)
        {
            writer.WriteNull();
            return;
        }

        var isRef = writer.Path != "uid" && !writer.Path.EndsWith(".uid");

        if (isRef)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("uid");
        }

        writer.WriteValue(uid.ToString());

        if (isRef)
        {
            writer.WriteEndObject();
        }
    }
}
