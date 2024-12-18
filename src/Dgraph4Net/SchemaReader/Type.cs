using System.Text.Json.Serialization;

namespace Dgraph4Net.SchemaReader;

internal class Type
{
    [JsonPropertyName("fields")]
    internal List<Field> Fields { get; set; }

    [JsonPropertyName("name")]
    internal string Name { get; set; }
}
