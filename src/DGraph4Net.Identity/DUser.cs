using System;
using System.Collections.Generic;
using System.Linq;
using DGraph4Net.Annotations;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace DGraph4Net.Identity
{
    [DGraphType("AspNetUser")]
    public class DUser : DUser<DRole, DUserClaim, DUserLogin, DUserToken>
    {
        [JsonProperty("roles"), ReversePredicate, PredicateReferencesTo(typeof(DRole)), JsonIgnore]
        public override ICollection<DRole> Roles { get; set; } = new List<DRole>();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S4035:Classes implementing \"IEquatable<T>\" should be sealed", Justification = "<Pending>")]
    public abstract class DUser<TRole, TUserClaim, TUserLogin, TUserToken> : IdentityUser<Uid>, IEntity, IEquatable<DUser<TRole, TUserClaim, TUserLogin, TUserToken>>
        where TRole : DRole
        where TUserClaim : DUserClaim
        where TUserLogin : DUserLogin
        where TUserToken : DUserToken
    {
        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        [JsonProperty("claims"), JsonIgnore]
        public virtual ICollection<TUserClaim> Claims { get; set; } = new List<TUserClaim>();

        public abstract ICollection<TRole> Roles { get; set; }

        [JsonProperty("logins"), JsonIgnore]
        public virtual ICollection<TUserLogin> Logins { get; set; } = new List<TUserLogin>();

        [JsonProperty("tokens"), JsonIgnore]
        public virtual ICollection<TUserToken> Tokens { get; set; } = new List<TUserToken>();

        private ICollection<string> _dType = new[] { "User" };

        [JsonProperty("dgraph.type")]
        public ICollection<string> DType
        {
            get
            {
                if (_dType.All(dt => dt != "User"))
                    _dType.Add("User");

                return _dType;
            }
            set
            {
                if (value.All(dt => dt != "User"))
                    value.Add("User");

                _dType = value;
            }
        }

        [PersonalData]
        [JsonProperty("password_hash"), StringPredicate]
        public override string PasswordHash { get => base.PasswordHash; set => base.PasswordHash = value; }

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

        internal void Populate(DUser usr)
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
            return obj.GetType() == GetType() && Equals((DUser<TRole, TUserClaim, TUserLogin, TUserToken>)obj);
        }

        public bool Equals(DUser<TRole, TUserClaim, TUserLogin, TUserToken> other) =>
            Id.Equals(other.Id);

        public static bool operator ==(DUser<TRole, TUserClaim, TUserLogin, TUserToken> usr, object other) =>
            usr?.Equals(other) == true;

        public static bool operator !=(DUser<TRole, TUserClaim, TUserLogin, TUserToken> usr, object other) =>
            usr?.Equals(other) != true;
    }
}
