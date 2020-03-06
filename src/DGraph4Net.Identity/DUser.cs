using System;
using System.Collections.Generic;
using System.Linq;
using Dgraph4Net.Annotations;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace Dgraph4Net.Identity
{
    [DgraphType("AspNetUser")]
    public class DUser : DUser<DUser, DRole, DRoleClaim, DUserClaim, DUserLogin, DUserToken>
    {
        [JsonProperty("roles"), ReversePredicate, PredicateReferencesTo(typeof(DRole))]
        public override ICollection<DRole> Roles { get; set; } = new List<DRole>();
        
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3400:Methods should not return constants", Justification = "<Pending>")]
        public virtual bool ShouldSerializeRoles() => false;
    }

    public abstract class DUser<TUser, TRole, TRoleClaim, TUserClaim, TUserLogin, TUserToken> : AEntity, IEquatable<DUser<TUser, TRole, TRoleClaim, TUserClaim, TUserLogin, TUserToken>>
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

        [JsonProperty("claims")]
        public virtual ICollection<TUserClaim> Claims { get; set; } = new List<TUserClaim>();
        
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3400:Methods should not return constants", Justification = "<Pending>")]
        public virtual bool ShouldSerializeClaims() => false;

        // ReSharper disable once UnusedMember.Global
        // ReSharper disable once UnusedMemberInSuper.Global
        public abstract ICollection<TRole> Roles { get; set; }

        [JsonProperty("logins")]
        public virtual ICollection<TUserLogin> Logins { get; set; } = new List<TUserLogin>();
        
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3400:Methods should not return constants", Justification = "<Pending>")]
        public virtual bool ShouldSerializeLogins() => false;

        [JsonProperty("tokens")]
        public virtual ICollection<TUserToken> Tokens { get; set; } = new List<TUserToken>();
        
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3400:Methods should not return constants", Justification = "<Pending>")]
        public virtual bool ShouldSerializeTokens() => false;

        [PersonalData]
        [JsonProperty("password_hash"), StringPredicate]
        public virtual string PasswordHash { get; set; }

        [JsonProperty("lockout_end"), DateTimePredicate(Token = DateTimeToken.Hour)]
        public virtual DateTimeOffset? LockoutEnd { get; set; }

        [PersonalData]
        [JsonProperty("two_factor_enabled"), CommonPredicate]
        public virtual bool TwoFactorEnabled { get; set; }

        [PersonalData]
        [JsonProperty("phonenumber_confirmed"), CommonPredicate]
        public virtual bool PhoneNumberConfirmed { get; set; }

        [ProtectedPersonalData]
        [JsonProperty("phonenumber"), StringPredicate]
        public virtual string PhoneNumber { get; set; }

        [JsonProperty("concurrency_stamp"), StringPredicate]
        public virtual string ConcurrencyStamp { get; set; }

        [JsonProperty("security_stamp"), StringPredicate]
        public virtual string SecurityStamp { get; set; }

        [PersonalData]
        [JsonProperty("email_confirmed"), CommonPredicate]
        public virtual bool EmailConfirmed { get; set; }

        [JsonProperty("normalized_email"), StringPredicate(Token = StringToken.Exact)]
        public virtual string NormalizedEmail { get; set; }

        [ProtectedPersonalData]
        [JsonProperty("email"), StringPredicate(Token = StringToken.Exact)]
        public virtual string Email { get; set; }

        [JsonProperty("normalized_username"), StringPredicate(Token = StringToken.Exact)]
        public virtual string NormalizedUserName { get; set; }

        [ProtectedPersonalData]
        [JsonProperty("username"), StringPredicate(Token = StringToken.Exact)]
        public virtual string UserName { get; set; }

        [JsonProperty("lockout_enabled"), CommonPredicate]
        public virtual bool LockoutEnabled { get; set; }

        [JsonProperty("access_failed_count"), CommonPredicate]
        public virtual int AccessFailedCount { get; set; }

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
