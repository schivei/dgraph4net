using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Dgraph4Net.Annotations;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace Dgraph4Net.Identity
{
    [DgraphType("AspNetRoleClaim")]
    public class DRoleClaim : DRoleClaim<DRoleClaim, DRole>
    {
        [JsonProperty("role_id"), PredicateReferencesTo(typeof(DRole)), CommonPredicate]
        public override Uid RoleId { get; set; }
    }

    public abstract class DRoleClaim<TRoleClaim, TRole> : AEntity
        where TRoleClaim : DRoleClaim<TRoleClaim, TRole>, new()
        where TRole : DRole<TRole, TRoleClaim>, new()
    {
        protected bool Equals(DRoleClaim<TRoleClaim, TRole> other)
        {
            return Id.Equals(other?.Id);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((DRoleClaim<TRoleClaim, TRole>) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        [JsonProperty("claim_value"), StringPredicate(Token = StringToken.Term, Fulltext = true)]
        public virtual string ClaimValue { get; set; }

        [JsonProperty("claim_type"), StringPredicate(Token = StringToken.Exact)]
        public virtual string ClaimType { get; set; }

        public abstract Uid RoleId { get; set; }

        public static TRoleClaim InitializeFrom(TRole role, Claim claim)
        {
            return new TRoleClaim
            {
                ClaimType = claim.Type,
                ClaimValue = claim.Value,
                Id = Uid.NewUid(),
                RoleId = role.Id
            };
        }

        public static bool operator ==(DRoleClaim<TRoleClaim, TRole> usr, object other) =>
            usr != null && usr.Equals(other);

        public static bool operator !=(DRoleClaim<TRoleClaim, TRole> usr, object other) =>
            !usr?.Equals(other) == true;
    }
}
