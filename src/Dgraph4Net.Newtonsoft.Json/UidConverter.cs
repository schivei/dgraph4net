using Newtonsoft.Json;

namespace Dgraph4Net;

internal class UidConverter : JsonConverter<Uid>
{
    private class Setter : IDisposable
    {
        private readonly JsonSerializer _serializer;
        private readonly int _index;
        private readonly JsonConverter _converter;

        public Setter(JsonSerializer serializer, JsonConverter converter)
        {
            _serializer = serializer;
            _converter = converter;
            _index = serializer.Converters.IndexOf(converter);

            serializer.Converters.RemoveAt(_index);
        }

        public void Dispose() => _serializer.Converters.Insert(_index, _converter);
    }

    private Setter GetSetter(JsonSerializer serializer) =>
        new(serializer, this);

    public override Uid ReadJson(JsonReader reader, Type objectType, Uid existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        string? value;
        using (GetSetter(serializer))
            value = reader.ReadAsString();

        if (value is null)
            return existingValue;

        if (reader.TokenType == JsonToken.String)
            return new(value);

        if (reader.TokenType == JsonToken.Integer)
            return new(ulong.Parse(value));

        if (reader.TokenType != JsonToken.StartObject)
            return existingValue;
        
        reader.Read();
        if (reader.TokenType != JsonToken.PropertyName || (string)reader.Value != "uid")
            return existingValue;
            
        reader.Read();

        if (reader.TokenType == JsonToken.String)
            return new(reader.Value.ToString());

        return reader.TokenType == JsonToken.Integer ?
            new(ulong.Parse(reader.Value.ToString())) :
            existingValue;
    }

    public override void WriteJson(JsonWriter writer, Uid value, JsonSerializer serializer)
    {
        if (value.IsEmpty)
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

        writer.WriteValue(value.ToString());

        if (isRef)
        {
            writer.WriteEndObject();
        }
    }
}
