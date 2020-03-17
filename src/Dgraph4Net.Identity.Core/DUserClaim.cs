using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Dgraph4Net.Annotations;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace Dgraph4Net.Identity
{
    [DgraphType("AspNetUserClaim")]
    public class DUserClaim : DUserClaim<DUserClaim, DUser>
    {
        [JsonProperty("user_id"), PredicateReferencesTo(typeof(DUser)), CommonPredicate]
        public override Uid UserId { get; set; }
    }

    public abstract class DUserClaim<TUserClaim, TUser> : AEntity
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

        [JsonProperty("claim_value"), StringPredicate(Token = StringToken.Term, Fulltext = true)]
        public virtual string ClaimValue { get; set; }

        [JsonProperty("claim_type"), StringPredicate(Token = StringToken.Exact)]
        public virtual string ClaimType { get; set; }

        public abstract Uid UserId { get; set; }

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
