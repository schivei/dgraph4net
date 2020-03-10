using System;
using Dgraph4Net.Annotations;
using Newtonsoft.Json;

namespace Dgraph4Net.Identity
{
    [DgraphType("AspNetUserToken")]
    public class DUserToken : DUserToken<DUserToken, DUser>
    {
        [JsonProperty("user"), PredicateReferencesTo(typeof(DUser)), CommonPredicate]
        public override Uid UserId { get; set; }
    }
}
