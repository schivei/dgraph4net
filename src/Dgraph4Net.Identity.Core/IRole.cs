using System.Collections.Generic;

namespace Dgraph4Net.Identity
{
    public interface IRole : IEntity
    {
        /// <summary>
        /// Gets or sets the name for this role.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Gets or sets the normalized name for this role.
        /// </summary>
        string NormalizedName { get; set; }

        /// <summary>
        /// A random value that should change whenever a role is persisted to the store
        /// </summary>
        string ConcurrencyStamp { get; set; }
        
        List<IRoleClaim> Claims { get; set; }
    }

    public interface IRole<TRoleClaim> : IRole where TRoleClaim : IRoleClaim, new()
    {
        new List<TRoleClaim> Claims { get; set; }
    }
}
