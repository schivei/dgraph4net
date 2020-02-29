using System;
using System.Collections.Generic;
using System.Linq;
using DGraph4Net.Annotations;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace DGraph4Net.Identity
{
    [DGraphType("AspNetUser")]
    public class DUser : DUser<DUser, DRole, DRoleClaim, DUserClaim, DUserLogin, DUserToken>
    {
        [JsonProperty("roles"), ReversePredicate, PredicateReferencesTo(typeof(DRole)), JsonIgnore]
        public override ICollection<DRole> Roles { get; set; } = new List<DRole>();
    }

    public abstract class DUser<TUser, TRole, TRoleClaim, TUserClaim, TUserLogin, TUserToken> : IdentityUser<Uid>, IEntity, IEquatable<DUser<TUser, TRole, TRoleClaim, TUserClaim, TUserLogin, TUserToken>>
        where TUser : DUser<TUser, TRole, TRoleClaim, TUserClaim, TUserLogin, TUserToken>, new()
        where TRole : DRole<TRole, TRoleClaim>, new()
        where TRoleClaim : DRoleClaim<TRoleClaim, TRole>, new()
        where TUserClaim : DUserClaim<TUserClaim, TUser>, new()
        where TUserLogin : DUserLogin<TUserLogin>, new()
        where TUserToken : DUserToken<TUserToken, TUser>, new()
    {
        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        [JsonProperty("claims"), JsonIgnore]
        public virtual ICollection<TUserClaim> Claims { get; set; } = new List<TUserClaim>();

        // ReSharper disable once UnusedMember.Global
        // ReSharper disable once UnusedMemberInSuper.Global
        public abstract ICollection<TRole> Roles { get; set; }

        [JsonProperty("logins"), JsonIgnore]
        public virtual ICollection<TUserLogin> Logins { get; set; } = new List<TUserLogin>();

        [JsonProperty("tokens"), JsonIgnore]
        public virtual ICollection<TUserToken> Tokens { get; set; } = new List<TUserToken>();

        protected DUser()
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

        [PersonalData]
        [JsonProperty("password_hash"), StringPredicate]
        public override string PasswordHash { get; set; }

        [JsonProperty("lockout_end"), DateTimePredicate(Token = DateTimeToken.Hour)]
        public override DateTimeOffset? LockoutEnd { get; set; }

        [PersonalData]
        [JsonProperty("two_factor_enabled"), CommonPredicate]
        public override bool TwoFactorEnabled { get; set; }

        [PersonalData]
        [JsonProperty("phonenumber_confirmed"), CommonPredicate]
        public override bool PhoneNumberConfirmed { get; set; }

        [ProtectedPersonalData]
        [JsonProperty("phonenumber"), StringPredicate]
        public override string PhoneNumber { get; set; }

        [JsonProperty("concurrency_stamp"), StringPredicate]
        public override string ConcurrencyStamp { get; set; }

        [JsonProperty("security_stamp"), StringPredicate]
        public override string SecurityStamp { get; set; }

        [PersonalData]
        [JsonProperty("email_confirmed"), CommonPredicate]
        public override bool EmailConfirmed { get; set; }

        [JsonProperty("normalized_email"), StringPredicate(Token = StringToken.Exact)]
        public override string NormalizedEmail { get; set; }

        [ProtectedPersonalData]
        [JsonProperty("email"), StringPredicate(Token = StringToken.Exact)]
        public override string Email { get; set; }

        [JsonProperty("normalized_username"), StringPredicate(Token = StringToken.Exact)]
        public override string NormalizedUserName { get; set; }

        [ProtectedPersonalData]
        [JsonProperty("username"), StringPredicate(Token = StringToken.Exact)]
        public override string UserName { get; set; }

        [PersonalData]
        [JsonProperty("uid")]
        public override Uid Id { get; set; }

        [JsonProperty("lockout_enabled"), CommonPredicate]
        public override bool LockoutEnabled { get; set; }

        [JsonProperty("access_failed_count"), CommonPredicate]
        public override int AccessFailedCount { get; set; }

        internal void Populate(TUser usr)
        {
            GetType().GetProperties()
                .Where(prop => prop.GetValue(usr) != null).ToList()
                .ForEach(prop => prop.SetValue(this, prop.GetValue(usr)));
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            return obj is TUser usr && Equals(usr);
        }

        public bool Equals(DUser<TUser, TRole, TRoleClaim, TUserClaim, TUserLogin, TUserToken> other) =>
            Id.Equals(other?.Id);

        public static bool operator ==(DUser<TUser, TRole, TRoleClaim, TUserClaim, TUserLogin, TUserToken> usr, object other) =>
            usr?.Equals(other) == true;

        public static bool operator !=(DUser<TUser, TRole, TRoleClaim, TUserClaim, TUserLogin, TUserToken> usr, object other) =>
            usr?.Equals(other) != true;
    }
}
