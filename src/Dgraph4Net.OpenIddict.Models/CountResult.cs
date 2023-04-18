using System.Text.Json.Serialization;

namespace Dgraph4Net.OpenIddict.Models;

public class CountResult : AEntity
{
    [JsonPropertyName("count")]
    public long Count { get; set; }
}
