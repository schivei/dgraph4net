using System;
using System.Collections.Generic;
using System.Linq;
using Dgraph4Net.Annotations;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace Dgraph4Net.Identity
{
    [DgraphType("AspNetUserLogin")]
    public class DUserLogin : DUserLogin<DUserLogin>
    {
        [JsonProperty("user_id"), PredicateReferencesTo(typeof(DUser)), CommonPredicate]
        public override Uid UserId { get; set; }
    }

    public abstract class DUserLogin<TUserLogin> : IdentityUserLogin<Uid>, IEntity, IEquatable<DUserLogin<TUserLogin>>
    where TUserLogin : DUserLogin<TUserLogin>
    {
        public bool Equals(DUserLogin<TUserLogin> other)
        {
            return Id.Equals(other?.Id);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            return obj.GetType() == GetType() && Equals((DUserLogin<TUserLogin>)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        [JsonProperty("uid")]
        public Uid Id { get; set; }

        protected DUserLogin()
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

        /// <summary>
        /// Gets or sets the login provider for the login (e.g. facebook, google)
        /// </summary>
        [JsonProperty("login_provider"), StringPredicate(Token = StringToken.Exact)]
        public override string LoginProvider { get; set; }

        /// <summary>
        /// Gets or sets the unique provider identifier for this login.
        /// </summary>
        [JsonProperty("provider_key"), StringPredicate(Token = StringToken.Exact)]
        public override string ProviderKey { get; set; }

        /// <summary>
        /// Gets or sets the friendly name used in a UI for this login.
        /// </summary>
        [JsonProperty("provider_display_name"), StringPredicate(Fulltext = true, Token = StringToken.Term)]
        public override string ProviderDisplayName { get; set; }

        /// <summary>
        /// Gets or sets the primary key of the user associated with this login.
        /// </summary>
        public abstract override Uid UserId { get; set; }

        public static bool operator ==(DUserLogin<TUserLogin> usr, object other) =>
            usr != null && usr.Equals(other);

        public static bool operator !=(DUserLogin<TUserLogin> usr, object other) =>
            !usr?.Equals(other) == true;
    }
}
