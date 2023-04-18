using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace System;

public class LocalizedStringsConverter : JsonConverter<LocalizedStrings>
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(LocalizedStrings);
    }

    public override LocalizedStrings? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var lss = new LocalizedStrings();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                continue;
            var key = reader.GetString();
            var value = reader.GetString();
            if (key is null || !key.Contains('@') || value is null)
                continue;

            lss.Add(new LocalizedString
            {
                LocalizedKey = key!,
                Value = value!
            });
        }

        return lss;
    }

    public override void Write(Utf8JsonWriter writer, LocalizedStrings value, JsonSerializerOptions options)
    {
        foreach (var ls in value)
        {
            writer.WriteString(ls.LocalizedKey, ls.Value);
        }
    }
}
