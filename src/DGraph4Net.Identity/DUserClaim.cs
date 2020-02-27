using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using DGraph4Net.Annotations;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace DGraph4Net.Identity
{
    [DGraphType("UserClaim")]
    public class DUserClaim : IdentityUserClaim<Uid>, IEntity
    {
        protected bool Equals(DUserClaim other)
        {
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((DUserClaim) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        private ICollection<string> _dType = new[] { "UserClaim" };

        [JsonProperty("dgraph.type")]
        public ICollection<string> DType
        {
            get
            {
                if (_dType.All(dt => dt != "UserClaim"))
                    _dType.Add("UserClaim");

                return _dType;
            }
            set
            {
                if (value.All(dt => dt != "UserClaim"))
                    value.Add("UserClaim");

                _dType = value;
            }
        }

        [JsonProperty("uid")]
        public new Uid Id { get => base.Id; set => base.Id = Convert.ToInt32(value.ToString().Substring(2), 16); }

        [JsonProperty("claim_value"), StringPredicate(Token = StringToken.Term, Fulltext = true)]
        public override string ClaimValue { get => base.ClaimValue; set => base.ClaimValue = value; }


        [JsonProperty("claim_type"), StringPredicate(Token = StringToken.Exact)]
        public override string ClaimType { get => base.ClaimType; set => base.ClaimType = value; }

        [JsonProperty("user_id"), PredicateReferencesTo(typeof(DUser)), CommonPredicate]
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

        public static bool operator ==(DUserClaim usr, object other) =>
            usr != null && usr.Equals(other);

        public static bool operator !=(DUserClaim usr, object other) =>
            !usr?.Equals(other) == true;
    }
}
