using System;
using System.Collections.Generic;
using System.Linq;
using DGraph4Net.Annotations;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace DGraph4Net.Identity
{
    [DGraphType("UserLogin")]
    public class DUserLogin : IdentityUserLogin<Uid>, IEntity
    {
        protected bool Equals(DUserLogin other)
        {
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            return obj.GetType() == GetType() && Equals((DUserLogin)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        [JsonProperty("uid")]
        public Uid Id { get; set; }

        private ICollection<string> _dType = new[] { "UserLogin" };

        [JsonProperty("dgraph.type")]
        public ICollection<string> DType
        {
            get
            {
                if (_dType.All(dt => dt != "UserLogin"))
                    _dType.Add("UserLogin");

                return _dType;
            }
            set
            {
                if (value.All(dt => dt != "UserLogin"))
                    value.Add("UserLogin");

                _dType = value;
            }
        }

        /// <summary>
        /// Gets or sets the login provider for the login (e.g. facebook, google)
        /// </summary>
        [JsonProperty("login_provider")]
        public override string LoginProvider { get => base.LoginProvider; set => base.LoginProvider = value; }

        /// <summary>
        /// Gets or sets the unique provider identifier for this login.
        /// </summary>
        [JsonProperty("provider_key")]
        public override string ProviderKey { get => base.ProviderKey; set => base.ProviderKey = value; }

        /// <summary>
        /// Gets or sets the friendly name used in a UI for this login.
        /// </summary>
        [JsonProperty("provider_display_name")]
        public override string ProviderDisplayName
        {
            get => base.ProviderDisplayName;
            set => base.ProviderDisplayName = value;
        }

        /// <summary>
        /// Gets or sets the primary key of the user associated with this login.
        /// </summary>
        [JsonProperty("user_id")]
        public override Uid UserId { get => base.UserId; set => base.UserId = value; }

        public static bool operator ==(DUserLogin usr, object other) =>
            usr != null && usr.Equals(other);

        public static bool operator !=(DUserLogin usr, object other) =>
            !usr?.Equals(other) == true;
    }
}
