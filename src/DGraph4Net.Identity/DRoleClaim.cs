using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using DGraph4Net.Annotations;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace DGraph4Net.Identity
{
    [DGraphType("RoleClaim")]
    public class DRoleClaim : IdentityRoleClaim<Uid>, IEntity
    {
        protected bool Equals(DRoleClaim other)
        {
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((DRoleClaim) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        private ICollection<string> _dType = new[] { "RoleClaim" };

        [JsonProperty("dgraph.type")]
        public ICollection<string> DType
        {
            get
            {
                if (_dType.All(dt => dt != "RoleClaim"))
                    _dType.Add("RoleClaim");

                return _dType;
            }
            set
            {
                if (value.All(dt => dt != "RoleClaim"))
                    value.Add("RoleClaim");

                _dType = value;
            }
        }

        [JsonProperty("uid")]
        public new Uid Id { get => base.Id; set => base.Id = Convert.ToInt32(value.ToString().Substring(2), 16); }

        [JsonProperty("claim_value"), StringPredicate(Token = StringToken.Term, Fulltext = true)]
        public override string ClaimValue { get => base.ClaimValue; set => base.ClaimValue = value; }

        [JsonProperty("claim_type"), StringPredicate(Token = StringToken.Exact)]
        public override string ClaimType { get => base.ClaimType; set => base.ClaimType = value; }

        [JsonProperty("role_id"), PredicateReferencesTo(typeof(DRole)), CommonPredicate]
        public override Uid RoleId { get => base.RoleId; set => base.RoleId = value; }

        public static TRoleClaim InitializeFrom<TRoleClaim>(DRole role, Claim claim) where TRoleClaim : DRoleClaim, new()
        {
            return new TRoleClaim
            {
                ClaimType = claim.Type,
                ClaimValue = claim.Value,
                Id = Uid.NewUid(),
                RoleId = role.Id
            };
        }

        public static bool operator ==(DRoleClaim usr, object other) =>
            usr != null && usr.Equals(other);

        public static bool operator !=(DRoleClaim usr, object other) =>
            !usr?.Equals(other) == true;
    }
}
