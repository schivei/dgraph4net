using System;
using System.Collections.Generic;

namespace Dgraph4Net.Identity;

public interface IUser : IEntity
{
    string PasswordHash { get; set; }
    DateTimeOffset? LockoutEnd { get; set; }
    bool TwoFactorEnabled { get; set; }
    bool PhoneNumberConfirmed { get; set; }
    string PhoneNumber { get; set; }
    string ConcurrencyStamp { get; set; }
    string SecurityStamp { get; set; }
    bool EmailConfirmed { get; set; }
    string NormalizedEmail { get; set; }
    string Email { get; set; }
    string NormalizedUserName { get; set; }
    string UserName { get; set; }
    bool LockoutEnabled { get; set; }
    int AccessFailedCount { get; set; }
    List<IUserClaim> Claims { get; set; }
    List<IRole> Roles { get; set; }
    List<IUserLogin> Logins { get; set; }
    List<IUserToken> Tokens { get; set; }
}

public interface IUser<TRole, TUserClaim, TUserLogin, TUserToken> : IUser where TRole : class, IRole, new() where TUserClaim : class, IUserClaim, new() where TUserLogin : class, IUserLogin, new() where TUserToken : class, IUserToken, new()
{
    new List<TUserClaim> Claims { get; set; }
    new List<TRole> Roles { get; set; }
    new List<TUserLogin> Logins { get; set; }
    new List<TUserToken> Tokens { get; set; }
}
