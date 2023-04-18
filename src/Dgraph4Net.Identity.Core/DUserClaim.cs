using System;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Dgraph4Net.Annotations;

namespace Dgraph4Net.Identity;

[DgraphType("AspNetUserClaim")]
public class DUserClaim : DUserClaim<DUserClaim, DUser>
{
}

public abstract class DUserClaim<TUserClaim, TUser> : AEntity, IUserClaim
    where TUserClaim : class, IUserClaim, new()
    where TUser : class, IUser, new()
{
    protected bool Equals(DUserClaim<TUserClaim, TUser> other)
    {
        return Id.Equals(other?.Id);
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((DUserClaim<TUserClaim, TUser>) obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id);
    }

    [JsonPropertyName("claim_value"), StringPredicate(Token = StringToken.Term, Fulltext = true)]
    public virtual string ClaimValue { get; set; }

    [JsonPropertyName("claim_type"), StringPredicate(Token = StringToken.Exact)]
    public virtual string ClaimType { get; set; }
    
    [JsonPropertyName("~claims")]
    public virtual Uid UserId { get; set; }

    public static TUserClaim InitializeFrom(TUser user, Claim claim)
    {
        var tr = new TUserClaim
        {
            ClaimType = claim.Type,
            ClaimValue = claim.Value
        };

        user.Claims.Add(tr);

        return tr;
    }

    public static bool operator ==(DUserClaim<TUserClaim, TUser> usr, object other) =>
        usr is not null && usr.Equals(other);

    public static bool operator !=(DUserClaim<TUserClaim, TUser> usr, object other) =>
        !usr?.Equals(other) == true;
}
