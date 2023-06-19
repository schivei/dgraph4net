using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

// ReSharper disable once CheckNamespace
namespace System;

internal partial class UidConverter : JsonConverterFactory
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

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == typeof(Uid))
        {
            return new JsonUidConverter();
        }

        if (typeToConvert == typeof(string))
        {
            return new JsonStringConverter();
        }

        return Activator.CreateInstance(typeof(JsonNumberConverter<>)
            .MakeGenericType(typeToConvert)) as JsonConverter;
    }

    [GeneratedRegex(@"^(\{)(.+)?(\"")([^\""]+)(\""\:)([^""]+)?$")]
    private static partial Regex UncompletedJson();

    private class JsonNumberConverter<T> : JsonConverter<T>
    {
        private readonly JsonUidConverter _uidConverter;

        public JsonNumberConverter()
        {
            _uidConverter = new JsonUidConverter();
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(T) && (
                   objectType == typeof(int) ||
                   objectType == typeof(uint) ||
                   objectType == typeof(long) ||
                   objectType == typeof(ulong) ||
                   objectType == typeof(byte) ||
                   objectType == typeof(char) ||
                   objectType == typeof(sbyte) ||
                   objectType == typeof(short) ||
                   objectType == typeof(ushort));
        }

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null || reader.TokenType == JsonTokenType.None)
            {
                return default;
            }

            if (reader.TokenType != JsonTokenType.Null || typeToConvert != typeof(T))
            {
                var jo = JsonDocument.ParseValue(ref reader).RootElement;
                if (jo.ValueKind == JsonValueKind.Number)
                {
                    var value = jo.GetRawText();
                    if (UncompletedJson().IsMatch(value))
                    {
                        var uid = _uidConverter.Read(ref reader, typeToConvert, options);

                        return (T)(object)(typeof(T) switch
                        {
                            Type t when t == typeof(int) => (int)uid,
                            Type t when t == typeof(uint) => (uint)uid,
                            Type t when t == typeof(long) => (long)uid,
                            Type t when t == typeof(ulong) => (ulong)uid,
                            Type t when t == typeof(byte) => (byte)uid,
                            Type t when t == typeof(char) => (char)uid,
                            Type t when t == typeof(sbyte) => (sbyte)uid,
                            Type t when t == typeof(short) => (short)uid,
                            Type t when t == typeof(ushort) => (ushort)uid,
                            _ => default
                        });
                    }

                    return (T)Convert.ChangeType(value, typeToConvert);
                }
            }

            return default;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            Uid uid = value is Uid u ? u : default;
            switch (value)
            {
                case int i:
                    uid = i;
                    break;
                case uint ui:
                    uid = ui;
                    break;
                case long l:
                    uid = l;
                    break;
                case ulong ul:
                    uid = ul;
                    break;
                case byte b:
                    uid = b;
                    break;
                case char c:
                    uid = c;
                    break;
                case sbyte sb:
                    uid = sb;
                    break;
                case short s:
                    uid = s;
                    break;
                case ushort us:
                    uid = us;
                    break;
            }

            _uidConverter.Write(writer, uid, options);
        }
    }

    private class JsonStringConverter : JsonConverter<string>
    {
        private readonly JsonUidConverter _uidConverter;

        public JsonStringConverter()
        {
            _uidConverter = new JsonUidConverter();
        }

        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try
            {
                return _uidConverter.Read(ref reader, typeToConvert, options);
            }
            catch (InvalidCastException)
            {
                var jo = JsonDocument.ParseValue(ref reader).RootElement;
                return jo.GetString();
            }
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            try
            {
                _uidConverter.Write(writer, value, options);
            }
            catch (InvalidCastException)
            {
                writer.WriteStringValue(value);
            }
        }
    }

    private class JsonUidConverter : JsonConverter<Uid>
    {
        public override Uid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            JsonElement? value = null;

            if (reader.TokenType == JsonTokenType.Null || reader.TokenType == JsonTokenType.None)
            {
                return default;
            }

            if (reader.TokenType != JsonTokenType.Null || typeToConvert != typeof(Uid))
            {
                var jo = JsonDocument.ParseValue(ref reader).RootElement;
                if (jo.ValueKind == JsonValueKind.Object)
                {
                    if (!jo.TryGetProperty("uid", out var tk) || tk.ValueKind == JsonValueKind.Null)
                    {
                        return default;
                    }

                    value = tk;
                }
                else
                {
                    value = jo;
                }
            }

            if (!value.HasValue || string.IsNullOrEmpty(value.Value.ToString()?.Trim()))
            {
                return default;
            }

            return value.Value.ValueKind switch
            {
                JsonValueKind.Object => new Uid((value.HasValue ? value.Value.Deserialize<Dictionary<string, JsonElement>>() : new())["uid"].GetString()),
                JsonValueKind.String => new Uid(value.Value.GetString()),
                JsonValueKind.Number => new Uid(value.Value.GetUInt64()),
                _ => default,
            };
        }

        public override void Write(Utf8JsonWriter writer, Uid value, JsonSerializerOptions options)
        {
            var uid = value.ToString();

            var ms = (Memory<byte>)writer.GetType().GetField("_memory", Reflection.BindingFlags.Instance | Reflection.BindingFlags.NonPublic)
                .GetValue(writer);

            var uncompletedJson = Encoding.UTF8.GetString(ms!.ToArray()); // i.e: { "prop": "value", "uid":
            var uncompletedPropertyName = UncompletedJson().Replace(uncompletedJson, "$4").ToLowerInvariant();

            var isRef = uncompletedPropertyName != "uid" && !uncompletedPropertyName.EndsWith(".uid");
            if (string.IsNullOrEmpty(uid?.Trim()))
            {
                writer.WriteNullValue();
                return;
            }

            if (isRef)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("uid");
            }

            writer.WriteStringValue(uid);

            if (isRef)
            {
                writer.WriteEndObject();
            }
        }
    }
}
