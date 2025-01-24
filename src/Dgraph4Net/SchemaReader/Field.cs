using System.Text.Json.Serialization;

namespace Dgraph4Net.SchemaReader;

internal class Field
{
    [JsonPropertyName("name")]
    internal string Name { get; set; }
}
