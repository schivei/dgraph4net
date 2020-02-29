using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using DGraph4Net.Annotations;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace DGraph4Net.Identity
{
    [DGraphType("AspNetRoleClaim")]
    public class DRoleClaim : DRoleClaim<DRoleClaim, DRole>
    {
        [JsonProperty("role_id"), PredicateReferencesTo(typeof(DRole)), CommonPredicate]
        public override Uid RoleId { get; set; }
    }

    public abstract class DRoleClaim<TRoleClaim, TRole> : IdentityRoleClaim<Uid>, IEntity
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

        protected DRoleClaim()
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
        public new Uid Id { get => base.Id; set => base.Id = Convert.ToInt32(value.ToString().Substring(2), 16); }

        [JsonProperty("claim_value"), StringPredicate(Token = StringToken.Term, Fulltext = true)]
        public override string ClaimValue { get; set; }

        [JsonProperty("claim_type"), StringPredicate(Token = StringToken.Exact)]
        public override string ClaimType { get; set; }

        public override Uid RoleId { get; set; }

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
