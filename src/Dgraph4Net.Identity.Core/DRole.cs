using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Dgraph4Net.Annotations;

namespace Dgraph4Net.Identity;

[DgraphType("AspNetRole")]
public class DRole : DRole<DRole, DRoleClaim>
{
}

public class DRole<TRole, TRoleClaim> : AEntity,
    IEquatable<DRole<TRole, TRoleClaim>>, IRole<TRoleClaim>
    where TRole : class, IRole<TRoleClaim>, new()
    where TRoleClaim : class, IRoleClaim, new()
{
    public bool Equals(DRole<TRole, TRoleClaim> other)
    {
        return Id.Equals(other?.Id);
    }

    public override bool Equals(object obj)
    {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        return obj.GetType() == GetType() && Equals((DRole<TRole, TRoleClaim>)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id);
    }

    [PredicateReferencesTo(typeof(IRoleClaim)), ReversePredicate, JsonPropertyName("claims")]
    public virtual List<TRoleClaim> Claims { get; set; } = new List<TRoleClaim>();

    /// <summary>
    /// Gets or sets the name for this role.
    /// </summary>
    [JsonPropertyName("rolename"), StringPredicate(Token = StringToken.Exact, Fulltext = true)]
    public virtual string Name { get; set; }

    /// <summary>
    /// Gets or sets the normalized name for this role.
    /// </summary>
    [JsonPropertyName("normalized_rolename"), StringPredicate(Token = StringToken.Exact)]
    public virtual string NormalizedName { get; set; }

    /// <summary>
    /// A random value that should change whenever a role is persisted to the store
    /// </summary>
    [JsonPropertyName("concurrency_stamp"), StringPredicate]
    public virtual string ConcurrencyStamp { get; set; }

    List<IRoleClaim> IRole.Claims
    {
        get => Claims.Cast<IRoleClaim>().ToList();
        set => Claims = value.Cast<TRoleClaim>().ToList();
    }

    public static bool operator ==(DRole<TRole, TRoleClaim> usr, object other) =>
        usr is not null && usr.Equals(other);

    public static bool operator !=(DRole<TRole, TRoleClaim> usr, object other) =>
        !usr?.Equals(other) == true;
}
