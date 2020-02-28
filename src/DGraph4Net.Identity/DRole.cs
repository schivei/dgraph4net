using System;
using System.Collections.Generic;
using System.Linq;
using DGraph4Net.Annotations;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace DGraph4Net.Identity
{
    [DGraphType("Role")]
    public class DRole : IdentityRole<Uid>, IEntity
    {
        protected bool Equals(DRole other)
        {
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((DRole) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        private ICollection<string> _dType = new[] { "Role" };

        [JsonProperty("dgraph.type")]
        public ICollection<string> DType
        {
            get
            {
                if (_dType.All(dt => dt != "Role"))
                    _dType.Add("Role");

                return _dType;
            }
            set
            {
                if (value.All(dt => dt != "Role"))
                    value.Add("Role");

                _dType = value;
            }
        }

        [JsonProperty("claims"), JsonIgnore]
        public virtual ICollection<DRoleClaim> Claims { get; set; } = new List<DRoleClaim>();

        /// <summary>
        /// Gets or sets the primary key for this role.
        /// </summary>
        [JsonProperty("uid")]
        public override Uid Id { get => base.Id; set => base.Id = value; }

        /// <summary>
        /// Gets or sets the name for this role.
        /// </summary>
        [JsonProperty("rolename"), StringPredicate(Token = StringToken.Exact)]
        public override string Name { get => base.Name; set => base.Name = value; }

        /// <summary>
        /// Gets or sets the normalized name for this role.
        /// </summary>
        [JsonProperty("normalized_rolename"), StringPredicate(Token = StringToken.Exact)]
        public override string NormalizedName { get => base.NormalizedName; set => base.NormalizedName = value; }

        /// <summary>
        /// A random value that should change whenever a role is persisted to the store
        /// </summary>
        [JsonProperty("concurrency_stamp"), StringPredicate]
        public override string ConcurrencyStamp { get => base.ConcurrencyStamp; set => base.ConcurrencyStamp = value; }

        internal static TRole Initialize<TRole>(DRole role) where TRole : DRole, new()
        {
            var tRole = new TRole();
            tRole.Populate(role);
            return tRole;
        }

        internal void Populate(DRole usr)
        {
            GetType().GetProperties().ToList()
                .ForEach(prop => prop.SetValue(this, prop.GetValue(usr)));
        }

        public static bool operator ==(DRole usr, object other) =>
            usr != null && usr.Equals(other);

        public static bool operator !=(DRole usr, object other) =>
            !usr?.Equals(other) == true;
    }
}
