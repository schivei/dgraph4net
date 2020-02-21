using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace DGraph4Net.Identity
{

    public class DUserClaim : IdentityUserClaim<Uid>, IEntity
    {
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

        [JsonProperty("uid")]
        public new Uid Id { get => base.Id; set => base.Id = Convert.ToInt32(value.ToString().Substring(2), 16); }

        [JsonProperty("claim_value")]
        public override string ClaimValue { get => base.ClaimValue; set => base.ClaimValue = value; }


        [JsonProperty("claim_type")]
        public override string ClaimType { get => base.ClaimType; set => base.ClaimType = value; }

        [JsonProperty("user_id")]
        public override Uid UserId { get => base.UserId; set => base.UserId = value; }

        public static TUserClaim InitializeFrom<TUserClaim>(DUser user, Claim claim) where TUserClaim : DUserClaim, new()
        {
            return new TUserClaim
            {
                ClaimType = claim.Type,
                ClaimValue = claim.Value,
                Id = Uid.NewUid(),
                UserId = user.Id
            };
        }
    }
}
