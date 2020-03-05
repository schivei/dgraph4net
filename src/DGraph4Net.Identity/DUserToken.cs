using System;
using System.Collections.Generic;
using System.Linq;
using Dgraph4Net.Annotations;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace Dgraph4Net.Identity
{
    [DgraphType("AspNetUserToken")]
    public class DUserToken : DUserToken<DUserToken, DUser>
    {
        [JsonProperty("user"), PredicateReferencesTo(typeof(DUser)), CommonPredicate]
        public override Uid UserId { get; set; }
    }

    public abstract class DUserToken<TUserToken, TUser> : AEntity, IEquatable<DUserToken<TUserToken, TUser>>
        where TUser : class, new()
        where TUserToken : DUserToken<TUserToken, TUser>, new()
    {
        public bool Equals(DUserToken<TUserToken, TUser> other)
        {
            return Id.Equals(other?.Id);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            return obj.GetType() == GetType() && Equals((DUserToken<TUserToken, TUser>)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        public abstract Uid UserId { get; set; }

        [JsonProperty("login_provider"), StringPredicate(Token = StringToken.Exact)]
        public virtual string LoginProvider { get; set; }

        [JsonProperty("name"), StringPredicate(Token = StringToken.Exact)]
        public virtual string Name { get; set; }

        [JsonProperty("value"), StringPredicate]
        [ProtectedPersonalData]
        public virtual string Value { get; set; }

        internal static TUserToken Initialize(DUserToken<TUserToken, TUser> userToken)
        {
            var t = new TUserToken();
            t.Populate(userToken);
            return t;
        }

        internal void Populate(DUserToken<TUserToken, TUser> usr)
        {
            GetType().GetProperties().ToList()
                .ForEach(prop => prop.SetValue(this, prop.GetValue(usr)));
        }

        public static bool operator ==(DUserToken<TUserToken, TUser> usr, object other) =>
            usr != null && usr.Equals(other);

        public static bool operator !=(DUserToken<TUserToken, TUser> usr, object other) =>
            !usr?.Equals(other) == true;
    }
}
