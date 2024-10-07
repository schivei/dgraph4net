using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace System;

internal partial class UidConverter : JsonConverter<Uid>
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

        if (value is null || string.IsNullOrWhiteSpace(value.Value.ToString()))
            return default;

        return value.Value.ValueKind switch
        {
            JsonValueKind.Object => new Uid((value.HasValue ? value.Value.Deserialize<Dictionary<string, JsonElement>>() : [])["uid"].GetString()),
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

        var uncompletedJson = Encoding.UTF8.GetString(ms!.ToArray()).ToString(); // i.e: { "prop": "value", "uid":
        var uncompletedPropertyName = UncompletedJson().Replace(uncompletedJson, "$2").ToLowerInvariant();

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

    [GeneratedRegex(@"^(.*"")([^""\s]+)("":.*)?$", RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    private static partial Regex UncompletedJson();
}
