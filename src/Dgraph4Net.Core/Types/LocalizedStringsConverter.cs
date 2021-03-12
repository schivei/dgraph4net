
using Newtonsoft.Json;

#nullable enable

namespace System
{
    public class LocalizedStringsConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(LocalizedStrings);
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            var lss = value as LocalizedStrings ?? new LocalizedStrings();

            foreach (var ls in lss)
            {
                writer.WritePropertyName(ls.LocalizedKey);
                writer.WriteValue(ls.Value);
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            var lss = existingValue as LocalizedStrings ?? new LocalizedStrings();

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    continue;

                var key = reader.Value?.ToString();
                var value = reader.ReadAsString();

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
    }
}
