using Newtonsoft.Json;

namespace Dgraph4Net;

internal static class JsonWriterExtension
{
    public static void WriteNull(this JsonWriter writer, string propertyName)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteNull();
    }
}