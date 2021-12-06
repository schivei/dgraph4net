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
    }

    public abstract class DUser<TUser, TRole, TRoleClaim, TUserClaim, TUserLogin, TUserToken> :
        AEntity, IEquatable<DUser<TUser, TRole, TRoleClaim, TUserClaim, TUserLogin, TUserToken>>,
        IUser<TRole, TUserClaim, TUserLogin, TUserToken>
        where TUser : class, IUser<TRole, TUserClaim, TUserLogin, TUserToken>, new()
        where TRole : class, IRole<TRoleClaim>, new()
        where TRoleClaim : class, IRoleClaim, new()
        where TUserClaim : class, IUserClaim, new()
        where TUserLogin : class, IUserLogin, new()
        where TUserToken : class, IUserToken, new()
    {
        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        [JsonProperty("claims"), ReversePredicate, PredicateReferencesTo(typeof(IUserClaim))]
        public virtual List<TUserClaim> Claims { get; set; } = new List<TUserClaim>();

        List<IRole> IUser.Roles
        {
            get => Roles.Cast<IRole>().ToList();
            set => Roles = value.Cast<TRole>().ToList();
        }

        List<IUserLogin> IUser.Logins
        {
            get => Logins.Cast<IUserLogin>().ToList();
            set => Logins = value.Cast<TUserLogin>().ToList();
        }

        List<IUserToken> IUser.Tokens
        {
            get => Tokens.Cast<IUserToken>().ToList();
            set => Tokens = value.Cast<TUserToken>().ToList();
        }

        // ReSharper disable once UnusedMember.Global
        // ReSharper disable once UnusedMemberInSuper.Global
        List<IUserClaim> IUser.Claims
        {
            get => Claims.Cast<IUserClaim>().ToList();
            set => Claims = value.Cast<TUserClaim>().ToList();
        }

        [JsonProperty("roles"), ReversePredicate, PredicateReferencesTo(typeof(IRole))]
        public virtual List<TRole> Roles { get; set; } = new List<TRole>();

        [JsonProperty("logins"), ReversePredicate, PredicateReferencesTo(typeof(IUserLogin))]
        public virtual List<TUserLogin> Logins { get; set; } = new List<TUserLogin>();
        
        [JsonProperty("tokens"), ReversePredicate, PredicateReferencesTo(typeof(IUserToken))]
        public virtual List<TUserToken> Tokens { get; set; } = new List<TUserToken>();
        
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
            usr is not null && usr.Equals(other) == true;

        public static bool operator !=(DUser<TUser, TRole, TRoleClaim, TUserClaim, TUserLogin, TUserToken> usr, object other) =>
            usr?.Equals(other) != true;
    }
}
