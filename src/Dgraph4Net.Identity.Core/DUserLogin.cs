using System;
using System.Text.Json.Serialization;
using Dgraph4Net.Annotations;

namespace Dgraph4Net.Identity;

[DgraphType("AspNetUserLogin")]
public class DUserLogin : DUserLogin<DUserLogin>
{
}

public abstract class DUserLogin<TUserLogin> : AEntity, IEquatable<DUserLogin<TUserLogin>>,
    IUserLogin where TUserLogin : class, IUserLogin, new()
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

    /// <summary>
    /// Gets or sets the login provider for the login (e.g. facebook, google)
    /// </summary>
    [JsonPropertyName("login_provider"), StringPredicate(Token = StringToken.Exact)]
    public virtual string LoginProvider { get; set; }

    /// <summary>
    /// Gets or sets the unique provider identifier for this login.
    /// </summary>
    [JsonPropertyName("provider_key"), StringPredicate(Token = StringToken.Exact)]
    public virtual string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets the friendly name used in a UI for this login.
    /// </summary>
    [JsonPropertyName("provider_display_name"), StringPredicate(Fulltext = true, Token = StringToken.Term)]
    public virtual string ProviderDisplayName { get; set; }

    public static bool operator ==(DUserLogin<TUserLogin> usr, object other) =>
        usr is not null && usr.Equals(other);

    public static bool operator !=(DUserLogin<TUserLogin> usr, object other) =>
        !usr?.Equals(other) == true;
}
