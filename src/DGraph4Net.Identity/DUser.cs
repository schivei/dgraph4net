using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace DGraph4Net.Identity
{
    public class DUser : IdentityUser<Uid>, IEntity
    {
        [JsonProperty("claims")]
        public virtual ICollection<DUserClaim> Claims { get; set; }

        [JsonProperty("roles")]
        public virtual ICollection<DRole> Roles { get; set; }

        [JsonProperty("logins")]
        public virtual ICollection<DUserLogin> Logins { get; set; }

        [JsonProperty("tokens")]
        public virtual ICollection<DUserToken> Tokens { get; set; }

        [JsonProperty("dgraph.type")]
        public ICollection<string> DType { get; internal set; }

        [PersonalData]
        [JsonProperty("password_hash")]
        public override string PasswordHash { get => base.PasswordHash; set => base.PasswordHash = value; }
    }
}
