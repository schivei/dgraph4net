using System;

namespace Dgraph4Net.Identity
{
    public class DUserRole
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

        public virtual Uid UserId { get; set; }

        public virtual Uid RoleId { get; set; }

        public static bool operator ==(DUserRole usr, object other) =>
            !(usr is null) && usr.Equals(other);

        public static bool operator !=(DUserRole usr, object other) =>
            !usr?.Equals(other) == true;
    }
}
