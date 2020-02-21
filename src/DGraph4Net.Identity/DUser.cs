using System;
using System.Collections.Generic;
using System.Linq;
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

        private ICollection<string> _dType = new[] { IdentityTypeNameOptions.UserTypeName };

        [JsonProperty("dgraph.type")]
        public ICollection<string> DType
        {
            get
            {
                if (_dType.All(dt => dt != IdentityTypeNameOptions.UserTypeName))
                    _dType.Add(IdentityTypeNameOptions.UserTypeName);

                return _dType;
            }
            set
            {
                if (value.All(dt => dt != IdentityTypeNameOptions.UserTypeName))
                    value.Add(IdentityTypeNameOptions.UserTypeName);

                _dType = value;
            }
        }

        [PersonalData]
        [JsonProperty("password_hash")]
        public override string PasswordHash { get => base.PasswordHash; set => base.PasswordHash = value; }

        [JsonProperty("lockout_end")]
        public override DateTimeOffset? LockoutEnd { get; set; }

        [PersonalData]
        [JsonProperty("two_factor_enabled")]
        public override bool TwoFactorEnabled { get; set; }

        [PersonalData]
        [JsonProperty("phonenumber_confirmed")]
        public override bool PhoneNumberConfirmed { get; set; }

        [ProtectedPersonalData]
        [JsonProperty("phonenumber")]
        public override string PhoneNumber { get; set; }

        [JsonProperty("concurrency_stamp")]
        public override string ConcurrencyStamp { get; set; }

        [JsonProperty("security_stamp")]
        public override string SecurityStamp { get; set; }

        [PersonalData]
        [JsonProperty("email_confirmed")]
        public override bool EmailConfirmed { get; set; }

        [JsonProperty("normalized_email")]
        public override string NormalizedEmail { get; set; }

        [ProtectedPersonalData]
        [JsonProperty("email")]
        public override string Email { get; set; }

        [JsonProperty("normalized_username")]
        public override string NormalizedUserName { get; set; }

        [ProtectedPersonalData]
        [JsonProperty("username")]
        public override string UserName { get; set; }

        [PersonalData]
        [JsonProperty("uid")]
        public override Uid Id { get; set; }

        [JsonProperty("lockout_enabled")]
        public override bool LockoutEnabled { get; set; }

        [JsonProperty("access_failed_count")]
        public override int AccessFailedCount { get; set; }
    }
}
