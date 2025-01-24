using System.Text.Json.Serialization;

namespace Dgraph4Net.SchemaReader;

internal class Schema
{
    [JsonPropertyName("predicate")]
    internal string Predicate { get; set; }
}
