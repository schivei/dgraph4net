using Newtonsoft.Json;

namespace Dgraph4Net.OpenIddict.Models
{
    public class CountResult : AEntity
    {
        [JsonProperty("count")]
        public long Count { get; set; }
    }
}
