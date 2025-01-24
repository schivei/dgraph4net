using Newtonsoft.Json;

namespace Dgraph4Net;

internal class Setter : IDisposable
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