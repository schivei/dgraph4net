using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dgraph4Net.Annotations;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace Dgraph4Net.Identity
{

    public abstract class DUserToken<TUserToken, TUser> : AEntity,
        IEquatable<DUserToken<TUserToken, TUser>>,
        IEqualityComparer<DUserToken<TUserToken, TUser>>,
        IEqualityComparer
        where TUserToken : DUserToken<TUserToken, TUser>, new()
        where TUser : class, new()
    {
        public abstract Uid UserId { get; set; }

        [JsonProperty("login_provider"), StringPredicate(Token = StringToken.Exact)]
        public virtual string LoginProvider { get; set; }

        [StringPredicate(Fulltext = true, Trigram = true, Token = StringToken.Term), JsonProperty("name")]
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
        
        public bool Equals(DUserToken<TUserToken, TUser> x, DUserToken<TUserToken, TUser> y) =>
            x?.Equals(y) ?? false;

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

        new public bool Equals(object x, object y)
        {
            if (x == y)
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            if (x is DUserToken<TUserToken, TUser> a
                && y is DUserToken<TUserToken, TUser> b)
            {
                return Equals(a, b);
            }

            throw new ArgumentException("", nameof(x));
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }
        
        public int GetHashCode(DUserToken<TUserToken, TUser> obj) =>
            obj.GetHashCode();

        public int GetHashCode(object obj)
        {
            if (obj == null)
            {
                return 0;
            }

            if (obj is DUserToken<TUserToken, TUser> x)
            {
                return GetHashCode(x);
            }

            throw new ArgumentException("", nameof(obj));
        }

        public static bool operator ==(DUserToken<TUserToken, TUser> usr, object other) =>
            usr?.Equals(other) == true;

        public static bool operator !=(DUserToken<TUserToken, TUser> usr, object other) =>
            usr?.Equals(other) == false;
    }
}
