using System;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace Dgraph4Net.Identity
{
    public class DUserRole : IdentityUserRole<Uid>
    {
        protected bool Equals(DUserRole other)
        {
            return UserId.Equals(other.UserId) && RoleId.Equals(other.RoleId);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((DUserRole) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(UserId, RoleId);
        }

        [JsonProperty("user_id")]
        public override Uid UserId { get => base.UserId; set => base.UserId = value; }

        [JsonProperty("role_id")]
        public override Uid RoleId { get => base.RoleId; set => base.RoleId = value; }

        public static bool operator ==(DUserRole usr, object other) =>
            usr != null && usr.Equals(other);

        public static bool operator !=(DUserRole usr, object other) =>
            !usr?.Equals(other) == true;
    }
}
