using System;
using System.Collections.Generic;
using System.Linq;
using Dgraph4Net.Annotations;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace Dgraph4Net.Identity
{
    [DgraphType("AspNetRole")]
    public class DRole : DRole<DRole, DRoleClaim>
    {
    }

    public class DRole<TRole, TRoleClaim> : AEntity, IEquatable<DRole<TRole, TRoleClaim>>
    where TRole : DRole<TRole, TRoleClaim>, new()
    where TRoleClaim : DRoleClaim<TRoleClaim, TRole>, new()
    {
        public bool Equals(DRole<TRole, TRoleClaim> other)
        {
            return Id.Equals(other?.Id);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((DRole<TRole, TRoleClaim>) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        [JsonProperty("claims")]
        public virtual ICollection<TRoleClaim> Claims { get; set; } = new List<TRoleClaim>();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3400:Methods should not return constants", Justification = "<Pending>")]
        public virtual bool ShouldSerializeClaims() => false;

        /// <summary>
        /// Gets or sets the name for this role.
        /// </summary>
        [JsonProperty("rolename"), StringPredicate(Token = StringToken.Exact, Fulltext = true)]
        public virtual string Name { get; set; }

        /// <summary>
        /// Gets or sets the normalized name for this role.
        /// </summary>
        [JsonProperty("normalized_rolename"), StringPredicate(Token = StringToken.Exact)]
        public virtual string NormalizedName { get; set; }

        /// <summary>
        /// A random value that should change whenever a role is persisted to the store
        /// </summary>
        [JsonProperty("concurrency_stamp"), StringPredicate]
        public virtual string ConcurrencyStamp { get; set; }

        internal static TRole Initialize(TRole role)
        {
            var tRole = new TRole();
            tRole.Populate(role);
            return tRole;
        }

        internal void Populate(TRole usr)
        {
            GetType().GetProperties().ToList()
                .ForEach(prop => prop.SetValue(this, prop.GetValue(usr)));
        }

        public static bool operator ==(DRole<TRole, TRoleClaim> usr, object other) =>
            usr != null && usr.Equals(other);

        public static bool operator !=(DRole<TRole, TRoleClaim> usr, object other) =>
            !usr?.Equals(other) == true;
    }
}
