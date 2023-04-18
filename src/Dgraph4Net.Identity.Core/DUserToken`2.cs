using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Dgraph4Net.Annotations;
using Microsoft.AspNetCore.Identity;

namespace Dgraph4Net.Identity;

public abstract class DUserToken<TUserToken, TUser> : AEntity,
    IEquatable<DUserToken<TUserToken, TUser>>,
    IEqualityComparer<DUserToken<TUserToken, TUser>>,
    IEqualityComparer, IUserToken where TUserToken : DUserToken<TUserToken, TUser>, new()
    where TUser : class, new()
{
    [JsonPropertyName("login_provider"), StringPredicate(Token = StringToken.Exact)]
    public virtual string LoginProvider { get; set; }

    [StringPredicate(Fulltext = true, Trigram = true, Token = StringToken.Term), JsonPropertyName("name")]
    public virtual string Name { get; set; }

    [JsonPropertyName("value"), StringPredicate]
    [ProtectedPersonalData]
    public virtual string Value { get; set; }

    public bool Equals(DUserToken<TUserToken, TUser> x, DUserToken<TUserToken, TUser> y) =>
        x?.Equals(y) ?? false;

    public bool Equals(DUserToken<TUserToken, TUser> other)
    {
        return Id.Equals(other?.Id);
    }

    public override bool Equals(object obj)
    {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        return obj.GetType() == GetType() && Equals((DUserToken<TUserToken, TUser>)obj);
    }

    public new bool Equals(object x, object y)
    {
        if (x == y)
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        if (x is DUserToken<TUserToken, TUser> a
            && y is DUserToken<TUserToken, TUser> b)
        {
            return Equals(a, b);
        }

        throw new ArgumentException("", nameof(x));
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(Id);
    }
    
    public int GetHashCode(DUserToken<TUserToken, TUser> obj) =>
        obj.GetHashCode();

    public int GetHashCode(object obj)
    {
        return obj switch
        {
            null => 0,
            DUserToken<TUserToken, TUser> x => GetHashCode(x),
            _ => throw new ArgumentException("", nameof(obj)),
        };
    }

    public static bool operator ==(DUserToken<TUserToken, TUser> usr, object other) =>
        usr is not null && usr.Equals(other) == true;

    public static bool operator !=(DUserToken<TUserToken, TUser> usr, object other) =>
        usr?.Equals(other) == false;
}
