using System.Text.Json.Serialization;

namespace Dgraph4Net.SchemaReader;

internal class Root
{
    [JsonPropertyName("schema")]
    internal List<Schema> Schema { get; set; }

    [JsonPropertyName("types")]
    internal List<Type> Types { get; set; }
}
