using System;
using System.Collections.Generic;
using System.Linq;
using DGraph4Net.Annotations;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace DGraph4Net.Identity
{
    [DGraphType("UserToken")]
    public class DUserToken : IdentityUserToken<Uid>, IEntity
    {
        protected bool Equals(DUserToken other)
        {
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((DUserToken) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        private ICollection<string> _dType = new[] { "UserToken" };

        [JsonProperty("dgraph.type")]
        public ICollection<string> DType
        {
            get
            {
                if (_dType.All(dt => dt != "UserToken"))
                    _dType.Add("UserToken");

                return _dType;
            }
            set
            {
                if (value.All(dt => dt != "UserToken"))
                    value.Add("UserToken");

                _dType = value;
            }
        }

        [JsonProperty("uid")]
        public virtual Uid Id { get; set; }

        [JsonProperty("user_id"), PredicateReferencesTo(typeof(DUser)), CommonPredicate]
        public override Uid UserId { get; set; }

        [JsonProperty("login_provider"), StringPredicate(Token = StringToken.Exact)]
        public override string LoginProvider { get; set; }

        [JsonProperty("name"), StringPredicate(Token = StringToken.Exact)]
        public override string Name { get; set; }

        [JsonProperty("value"), StringPredicate]
        [ProtectedPersonalData]
        public override string Value { get; set; }

        internal static TUserToken Initialize<TUserToken>(DUserToken userToken) where TUserToken : DUserToken, new()
        {
            var t = new TUserToken();
            t.Populate(userToken);
            return t;
        }

        internal void Populate(DUserToken usr)
        {
            GetType().GetProperties().ToList()
                .ForEach(prop => prop.SetValue(this, prop.GetValue(usr)));
        }

        public static bool operator ==(DUserToken usr, object other) =>
            usr != null && usr.Equals(other);

        public static bool operator !=(DUserToken usr, object other) =>
            !usr?.Equals(other) == true;
    }
}
