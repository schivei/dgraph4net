using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using DGraph4Net.Annotations;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace DGraph4Net.Identity
{
    [DGraphType("AspNetUserClaim")]
    public class DUserClaim : DUserClaim<DUserClaim, DUser>
    {
        [JsonProperty("user_id"), PredicateReferencesTo(typeof(DUser)), CommonPredicate]
        public override Uid UserId { get; set; }
    }

    public abstract class DUserClaim<TUserClaim, TUser> : IdentityUserClaim<Uid>, IEntity
        where TUserClaim : DUserClaim<TUserClaim, TUser>, new()
        where TUser : IEntity, new()
    {
        protected bool Equals(DUserClaim<TUserClaim, TUser> other)
        {
            return Id.Equals(other?.Id);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((DUserClaim<TUserClaim, TUser>) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        protected DUserClaim()
        {
            _dType = new[] { this.GetDType() };
        }

        private ICollection<string> _dType;

        [JsonProperty("dgraph.type")]
        public ICollection<string> DType
        {
            get
            {
                var dtype = this.GetDType();
                if (_dType.All(dt => dt != dtype))
                    _dType.Add(dtype);

                return _dType;
            }
            set
            {
                var dtype = this.GetDType();
                if (value.All(dt => dt != dtype))
                    value.Add(dtype);

                _dType = value;
            }
        }

        [JsonProperty("uid")]
        public new Uid Id { get; set; }

        [JsonProperty("claim_value"), StringPredicate(Token = StringToken.Term, Fulltext = true)]
        public override string ClaimValue { get; set; }

        [JsonProperty("claim_type"), StringPredicate(Token = StringToken.Exact)]
        public override string ClaimType { get; set; }

        public override Uid UserId { get; set; }

        public static TUserClaim InitializeFrom(TUser user, Claim claim)
        {
            return new TUserClaim
            {
                ClaimType = claim.Type,
                ClaimValue = claim.Value,
                Id = Uid.NewUid(),
                UserId = user.Id
            };
        }

        public static bool operator ==(DUserClaim<TUserClaim, TUser> usr, object other) =>
            usr != null && usr.Equals(other);

        public static bool operator !=(DUserClaim<TUserClaim, TUser> usr, object other) =>
            !usr?.Equals(other) == true;
    }
}
