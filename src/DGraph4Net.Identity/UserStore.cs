using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Api;

using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Dgraph4Net.Identity
{
    /// <summary>
    /// Represents a new instance of a persistence store for users, using the default implementation
    /// of <see cref="IdentityUser{Uid}"/> with a string as a primary key.
    /// </summary>
    public class UserStore : UserStore<DUser, DRole, DUserClaim, DUserRole, DUserLogin, DUserToken, DRoleClaim>
    {
        /// <summary>
        /// Constructs a new instance of <see cref="UserStore"/>.
        /// </summary>
        /// <param name="context">The <see cref="Dgraph4NetClient"/>.</param>
        /// <param name="logger"></param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/>.</param>
        public UserStore(Dgraph4NetClient context, ILogger<UserStore> logger, IdentityErrorDescriber describer = null) :
            base(context, logger, describer)
        { }
    }

    /// <summary>
    /// Represents a new instance of a persistence store for the specified user and role types.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    /// <typeparam name="TRole">The type representing a role.</typeparam>
    /// <typeparam name="TUserClaim">The type representing a claim.</typeparam>
    /// <typeparam name="TUserRole">The type representing a user role.</typeparam>
    /// <typeparam name="TUserLogin">The type representing a user external login.</typeparam>
    /// <typeparam name="TUserToken">The type representing a user token.</typeparam>
    /// <typeparam name="TRoleClaim">The type representing a role claim.</typeparam>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
    // ReSharper disable once UnusedTypeParameter
    public class UserStore<TUser, TRole, TUserClaim, TUserRole, TUserLogin, TUserToken, TRoleClaim> :
        IUserLoginStore<TUser>,
        IUserClaimStore<TUser>,
        IUserPasswordStore<TUser>,
        IUserSecurityStampStore<TUser>,
        IUserEmailStore<TUser>,
        IUserLockoutStore<TUser>,
        IUserPhoneNumberStore<TUser>,
        IUserTwoFactorStore<TUser>,
        IUserAuthenticationTokenStore<TUser>,
        IUserAuthenticatorKeyStore<TUser>,
        IUserTwoFactorRecoveryCodeStore<TUser>,
        IProtectedUserStore<TUser>,
        IAsyncDisposable,
        IUserRoleStore<TUser>
        where TUser : class, IUser<TRole, TUserClaim, TUserLogin, TUserToken>, new()
        where TRole : class, IRole<TRoleClaim>, new()
        where TUserClaim : class, IUserClaim, new()
        where TUserRole : DUserRole, new()
        where TUserLogin : class, IUserLogin, new()
        where TUserToken : class, IUserToken, new()
        where TRoleClaim : class, IRoleClaim, new()
    {
        private readonly ILogger<UserStore<TUser, TRole, TUserClaim, TUserRole, TUserLogin, TUserToken, TRoleClaim>> _logger;
        private bool _disposed;

        /// <summary>
        /// Gets the database context for this store.
        /// </summary>
        public virtual Dgraph4NetClient Context { get; }

        private Txn GetTransaction(CancellationToken cancellationToken = default) =>
            Context.NewTransaction(cancellationToken: cancellationToken);

        /// <summary>
        /// Creates a new instance of the store.
        /// </summary>
        /// <param name="context">The context used to access the store.</param>
        /// <param name="logger"></param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to describe store errors.</param>
        public UserStore(Dgraph4NetClient context, ILogger<UserStore<TUser, TRole, TUserClaim, TUserRole, TUserLogin,
            TUserToken, TRoleClaim>> logger, IdentityErrorDescriber describer = null) :
            this(logger, describer ?? new IdentityErrorDescriber()) =>
            Context = context ?? throw new ArgumentNullException(nameof(context));

        private UserStore(IdentityErrorDescriber describer) =>
            ErrorDescriber = describer;

        public IdentityErrorDescriber ErrorDescriber { get; }

        private UserStore(ILogger<UserStore<TUser, TRole, TUserClaim, TUserRole, TUserLogin,
            TUserToken, TRoleClaim>> logger, IdentityErrorDescriber describer) :
            this(describer) => _logger = logger;

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UserStore<TUser, TRole, TUserClaim, TUserRole, TUserLogin, TUserToken, TRoleClaim>));
        }

        /// <summary>
        /// Throws if this class or context has been disposed.
        /// </summary>
        /// <param name="cancellationToken"></param>
        protected void ThrowIfDisposed(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Context?.IsDisposed() == true)
                throw new ObjectDisposedException(nameof(Context));

            ThrowIfDisposed();
        }

        public virtual Task<string> GetUserIdAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            return Task.FromResult(user.Id.ToString());
        }

        public virtual Task<string> GetUserNameAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            return Task.FromResult(user.UserName);
        }

        public virtual async Task SetUserNameAsync(TUser user, string userName, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            var usr = await FindByNameAsync(userName.ToUpperInvariant(), cancellationToken)
                .ConfigureAwait(false);

            if (!(usr is null) && usr.Id != user.Id)
                throw new AmbiguousMatchException("Name already exists.");

            user.UserName = userName;
        }

        public virtual Task<string> GetNormalizedUserNameAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            return Task.FromResult(user.NormalizedUserName);
        }

        public virtual async Task SetNormalizedUserNameAsync(TUser user, string normalizedName, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            var usr = await FindByNameAsync(normalizedName, cancellationToken)
                .ConfigureAwait(false);

            if (!(usr is null) && usr.Id != user.Id)
                throw new AmbiguousMatchException("Name already exists.");

            user.NormalizedUserName = normalizedName;
        }

        /// <summary>
        /// Finds and returns a user, if any, who has the specified <paramref name="userId"/>.
        /// </summary>
        /// <param name="userId">The user ID to search for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the user matching the specified <paramref name="userId"/> if it exists.
        /// </returns>
        public virtual async Task<TUser> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed(cancellationToken);
            CheckNull(userId, nameof(userId));

            Uid id = userId;

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars(@"query Q($userId: string) {
                    user(func: uid($userId)) {
                        uid
                        expand(_all_)
                        roles {
                              uid
                              expand(_all_)
                        }
                    }
                }", new Dictionary<string, string> { { "$userId", id } })
                .ConfigureAwait(false);

            return JsonConvert.DeserializeObject<Dictionary<string, List<TUser>>>(userResp.Json.ToStringUtf8())
                .First(x => x.Key == "user").Value?.FirstOrDefault();
        }

        /// <summary>
        /// Finds and returns a user, if any, who has the specified normalized user name.
        /// </summary>
        /// <param name="normalizedUserName">The normalized user name to search for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the user matching the specified <paramref name="normalizedUserName"/> if it exists.
        /// </returns>
        public virtual async Task<TUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars(@"query Q($userName: string) {
                    user(func: eq(normalized_username, $userName)) {
                        uid
                        expand(_all_)
                        roles {
                              uid
                              expand(_all_)
                        }
                    }
                }", new Dictionary<string, string> { { "$userName", normalizedUserName } })
                .ConfigureAwait(false);

            return JsonConvert.DeserializeObject<Dictionary<string, List<TUser>>>(userResp.Json.ToStringUtf8())
                .First(x => x.Key == "user").Value?.FirstOrDefault();
        }

        /// <summary>
        /// Return a role with the normalized name if it exists.
        /// </summary>
        /// <param name="normalizedRoleName">The normalized role name.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The role if it exists.</returns>
        public virtual async Task<TRole> FindRoleAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var resp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars(@"query Q($roleName: string) {
                    role(func: eq(normalized_rolename, $roleName)) {
                        uid
                        expand(_all_)
                    }
                }", new Dictionary<string, string> { { "$roleName", normalizedRoleName } })
                .ConfigureAwait(false);

            return JsonConvert.DeserializeObject<Dictionary<string, List<TRole>>>(resp.Json.ToStringUtf8())
                .First(x => x.Key == "role").Value?.FirstOrDefault();
        }

        /// <summary>
        /// Return a user role for the userId and roleId if it exists.
        /// </summary>
        /// <param name="userId">The user's id.</param>
        /// <param name="roleId">The role's id.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user role if it exists.</returns>
        // ReSharper disable once UnusedMember.Global
        public virtual async Task<TUserRole> FindUserRoleAsync(Uid userId, Uid roleId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var resp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars(@"query Q($userId: string, $roleId: string) {
                    userRole(func: uid($userId)) @normalize {
                        user_id: uid
                        roles @filter(uid($roleId)) {
                            role_id: uid
                        }
                    }
                }", new Dictionary<string, string>
                {
                    { "$userId", userId },
                    { "$roleId", roleId }
            }).ConfigureAwait(false);

            return JsonConvert.DeserializeObject<Dictionary<string, List<TUserRole>>>(resp.Json.ToStringUtf8())
                .First(x => x.Key == "userRole").Value?.FirstOrDefault();
        }

        /// <summary>
        /// Return a user with the matching userId if it exists.
        /// </summary>
        /// <param name="userId">The user's id.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user if it exists.</returns>
        public virtual async Task<TUser> FindUserAsync(Uid userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars(@"query Q($userId: string) {
                    user(func: uid($userId)) {
                        uid
                        expand(_all_)
                        roles {
                              uid
                              expand(_all_)
                        }
                    }
                }", new Dictionary<string, string> { { "$userId", userId } })
                .ConfigureAwait(false);

            return JsonConvert.DeserializeObject<Dictionary<string, List<TUser>>>(userResp.Json.ToStringUtf8())
                .First(x => x.Key == "user").Value?.FirstOrDefault();
        }

        /// <summary>
        /// Return a user login with the matching userId, provider, providerKey if it exists.
        /// </summary>
        /// <param name="userId">The user's id.</param>
        /// <param name="loginProvider">The login provider name.</param>
        /// <param name="providerKey">The key provided by the <paramref name="loginProvider"/> to identify a user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user login if it exists.</returns>
        public virtual async Task<TUserLogin> FindUserLoginAsync(Uid userId, string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var user = await FindByIdAsync(userId, cancellationToken);

            return user.Logins.Find(ul => ul.LoginProvider == loginProvider &&
                                                      ul.ProviderKey == providerKey);
        }

        /// <summary>
        /// Return a user login with  provider, providerKey if it exists.
        /// </summary>
        /// <param name="loginProvider">The login provider name.</param>
        /// <param name="providerKey">The key provided by the <paramref name="loginProvider"/> to identify a user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user login if it exists.</returns>
        public virtual async Task<TUserLogin> FindUserLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars(@"query Q($loginProvider: string, $providerKey: string) {
                    userLogin(func: eq(login_provider, $loginProvider)) @filter(eq(provider_key, $providerKey)) {
                        uid
                        expand(_all_)
                    }
                }", new Dictionary<string, string>
                {
                    { "$loginProvider", loginProvider },
                    { "$providerKey", providerKey }
                }).ConfigureAwait(false);

            var userLogin = JsonConvert.DeserializeObject<Dictionary<string, List<TUserLogin>>>(userResp.Json.ToStringUtf8())
                .First(x => x.Key == "userLogin").Value?.FirstOrDefault();

            return userLogin;
        }

        /// <summary>
        /// Retrieves the roles the specified <paramref name="user"/> is a member of.
        /// </summary>
        /// <param name="user">The user whose roles should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that contains the roles the user is a member of.</returns>
        public virtual async Task<IList<string>> GetRolesAsync(TUser user, CancellationToken cancellationToken = default)
        {
            var userRoles = await GetUserRolesAsync(user, cancellationToken)
                .ConfigureAwait(false);

            return userRoles.Select(x => x.NormalizedName).ToList();
        }

        public virtual async Task<IList<TRole>> GetUserRolesAsync(TUser user,
            CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);

            if (!(user.Roles is null) && user.Roles.Count > 0)
                return user.Roles;

            var usr = await FindByIdAsync(user.Id, cancellationToken)
                .ConfigureAwait(false);

            user.Roles = usr.Roles;

            return user.Roles;
        }

        /// <summary>
        /// Returns a flag indicating if the specified user is a member of the give <paramref name="normalizedRoleName"/>.
        /// </summary>
        /// <param name="user">The user whose role membership should be checked.</param>
        /// <param name="normalizedRoleName">The role to check membership of</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> containing a flag indicating if the specified user is a member of the given group. If the
        /// user is a member of the group the returned value with be true, otherwise it will be false.</returns>
        // ReSharper disable once UnusedMember.Global
        public virtual async Task<bool> IsInRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);
            CheckNull(normalizedRoleName, nameof(normalizedRoleName));

            var userRoles = await GetRolesAsync(user, cancellationToken)
                .ConfigureAwait(false);

            return userRoles.Any(x => x == normalizedRoleName);
        }

        /// <summary>
        /// Get the claims associated with the specified <paramref name="user"/> as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose claims should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that contains the claims granted to a user.</returns>
        public virtual async Task<IList<Claim>> GetClaimsAsync(TUser user, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);

            if (!(user.Claims is null))
                return user.Claims.Select(c => new Claim(c.ClaimType, c.ClaimValue)).ToList();

            var uctn = new TUserClaim().GetDType();

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars($@"query Q($userId: string) {{
                    claims(func: type({uctn})) @filter(uid_in(user_id, $userId)) {{
                        uid
                        expand(_all_) {{
                            uid
                        }}
                        dgraph.type
                    }}
                }}", new Dictionary<string, string> { { "$userId", user.Id } })
                .ConfigureAwait(false);

            user.Claims = JsonConvert.DeserializeObject<Dictionary<string, List<TUserClaim>>>
                    (userResp.Json.ToStringUtf8())
                .First(x => x.Key == "claims").Value ?? new List<TUserClaim>();

            return user.Claims.Select(c => new Claim(c.ClaimType, c.ClaimValue)).ToList();
        }

        /// <summary>
        /// Retrieves the associated logins for the specified <param ref="user"/>.
        /// </summary>
        /// <param name="user">The user whose associated logins to retrieve.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> for the asynchronous operation, containing a list of <see cref="UserLoginInfo"/> for the specified <paramref name="user"/>, if any.
        /// </returns>
        public virtual async Task<IList<UserLoginInfo>> GetLoginsAsync(TUser user, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);

            if (!(user.Logins is null))
                return user.Logins.Select(c => new UserLoginInfo(c.LoginProvider, c.ProviderKey, c.ProviderDisplayName)).ToList();

            var ultn = new TUserLogin().GetDType();

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars($@"query Q($userId: string) {{
                    logins(func: type({ultn})) @filter(uid_in(user_id, $userId)) {{
                        uid
                        expand(_all_) {{
                            uid
                        }}
                        dgraph.type
                    }}
                }}", new Dictionary<string, string> { { "$userId", user.Id } })
                .ConfigureAwait(false);

            user.Logins = JsonConvert.DeserializeObject<Dictionary<string, List<TUserLogin>>>
                                  (userResp.Json.ToStringUtf8())
                              .First(x => x.Key == "logins").Value ??
                          new List<TUserLogin>();

            return user.Logins.Select(c => new UserLoginInfo(c.LoginProvider, c.ProviderKey, c.ProviderDisplayName)).ToList();
        }

        /// <summary>
        /// Retrieves the user associated with the specified login provider and login provider key.
        /// </summary>
        /// <param name="loginProvider">The login provider who provided the <paramref name="providerKey"/>.</param>
        /// <param name="providerKey">The key provided by the <paramref name="loginProvider"/> to identify a user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> for the asynchronous operation, containing the user, if any which matched the specified login provider and key.
        /// </returns>
        public virtual async Task<TUser> FindByLoginAsync(string loginProvider, string providerKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars(@"query Q($loginProvider: string, $providerKey: string) {
                    userLogin(func: eq(login_provider, $loginProvider)) @filter(eq(provider_key, $providerKey)) @normalize {
                        ~logins {
                            uid
                        }
                    }
                }", new Dictionary<string, string>
                {
                    { "$loginProvider", loginProvider },
                    { "$providerKey", providerKey }
                }).ConfigureAwait(false);

            var user = JsonConvert.DeserializeObject<Dictionary<string, List<TUser>>>(userResp.Json.ToStringUtf8())
                .First(x => x.Key == "userLogin").Value?.FirstOrDefault();

            if (user is null)
                return null;

            return await FindByIdAsync(user.Id, cancellationToken);
        }

        public virtual async Task SetEmailAsync(TUser user, string email, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            var usr = await FindByEmailAsync(email.ToUpperInvariant(), cancellationToken)
                .ConfigureAwait(false);

            if (!(usr is null) && usr.Id != user.Id)
                throw new AmbiguousMatchException("Email already exists.");

            user.Email = email;
        }

        public virtual async Task<string> GetEmailAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            if (!(user.Email is null))
                return user.Email;

            var usr = await FindByIdAsync(user.Id, cancellationToken)
                .ConfigureAwait(false);

            user.Email = usr.Email;

            return user.Email;
        }

        public virtual Task<bool> GetEmailConfirmedAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            return Task.FromResult(user.EmailConfirmed);
        }

        public virtual Task SetEmailConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            user.EmailConfirmed = confirmed;

            return UpdateAsync(user, cancellationToken);
        }

        /// <summary>
        /// Gets the user, if any, associated with the specified, normalized email address.
        /// </summary>
        /// <param name="normalizedEmail">The normalized email address to return the user for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The task object containing the results of the asynchronous lookup operation, the user if any associated with the specified normalized email address.
        /// </returns>
        public virtual async Task<TUser> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var utn = new TUser().GetDType();

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars($@"query Q($userEmail: string) {{
                    user(func: type({utn})) @filter(eq(normalized_email, $userEmail)) {{
                        uid
                        expand(_all_) {{
                            uid
                        }}
                        roles {{
                            uid
                            expand(_all_) {{
                                uid
                            }}
                        }}
                        dgraph.type
                    }}
                }}", new Dictionary<string, string> { { "$userEmail", normalizedEmail } })
                .ConfigureAwait(false);

            return JsonConvert.DeserializeObject<Dictionary<string, List<TUser>>>(userResp.Json.ToStringUtf8())
                .First(x => x.Key == "user").Value?.FirstOrDefault();
        }

        public virtual async Task<string> GetNormalizedEmailAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            if (!(user.NormalizedUserName is null))
                return user.NormalizedUserName;

            var usr = await FindByIdAsync(user.Id, cancellationToken)
                .ConfigureAwait(false);

            user.NormalizedUserName = usr.NormalizedUserName;

            return user.NormalizedUserName;
        }

        public virtual async Task SetNormalizedEmailAsync(TUser user, string normalizedEmail, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            var usr = await FindByEmailAsync(normalizedEmail, cancellationToken)
                .ConfigureAwait(false);

            if (!(usr is null) && usr.Id != user.Id)
                throw new AmbiguousMatchException("Email already exists.");

            user.NormalizedEmail = normalizedEmail;
        }

        private async Task<IList<TUser>> GetUsersById(IEnumerable<Uid> uids, CancellationToken cancellationToken)
        {
            var utn = new TUser().GetDType();

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .Query($@"{{
                    users(func: uid({string.Join(',', uids)})) @filter(type({utn})) {{
                        uid
                        expand(_all_) {{
                            uid
                        }}
                        roles {{
                            uid
                            expand(_all_) {{
                                uid
                            }}
                        }}
                        dgraph.type
                    }}
                }}").ConfigureAwait(false);

            var usersResp = JsonConvert.DeserializeObject<Dictionary<string, List<TUser>>>(userResp.Json.ToStringUtf8());

            return !usersResp.ContainsKey("users") ? new List<TUser>() : usersResp["users"];
        }

        /// <summary>
        /// Retrieves all users with the specified claim.
        /// </summary>
        /// <param name="claim">The claim whose users should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> contains a list of users, if any, that contain the specified claim.
        /// </returns>
        public virtual async Task<IList<TUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            CheckNull(claim, nameof(claim));

            var uctn = new TUserClaim().GetDType();

            var q = $@"
            query Q($claimType: string, $claimValue: string) {{
                claims(func: eq(claim_type, $claimType)) @filter(type({uctn}) AND eq(claim_value, $claimValue))) @normalize {{
                    user_id {{
                        user_id: uid
                    }}
                }}
            }}";

            var response = await GetTransaction(cancellationToken).QueryWithVars(q,
                new Dictionary<string, string> {
                    { "$claimType", claim.Type },
                    { "$claimValue", claim.Value }
                }).ConfigureAwait(false);

            var claimsUser = JsonConvert.DeserializeObject<Dictionary<string, List<Dictionary<string, Uid>>>>(response.Json.ToStringUtf8());

            if (!claimsUser.ContainsKey("claims"))
                return new List<TUser>();

            var uids =
            claimsUser["claims"].Where(x => x.ContainsKey("user_id"))
                .Select(x => x["user_id"]);

            return await GetUsersById(uids, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves all users in the specified role.
        /// </summary>
        /// <param name="normalizedRoleName">The role whose users should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> contains a list of users, if any, that are in the specified role.
        /// </returns>
        // ReSharper disable once UnusedMember.Global
        public virtual async Task<IList<TUser>> GetUsersInRoleAsync(string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            CheckNull(normalizedRoleName, nameof(normalizedRoleName));

            var rtn = new TRole().GetDType();

            var q = $@"
            query Q($normalizedRoleName: string) {{
                uids(func: eq(normalized_rolename, $normalizedRoleName)) @filter(type({rtn})) @normalize {{
                    ~roles {{
                        user_id: uid
                    }}
                }}
            }}";

            var response = await GetTransaction(cancellationToken).QueryWithVars(q,
                new Dictionary<string, string> { { "$normalizedRoleName", normalizedRoleName } })
                .ConfigureAwait(false);

            var claimsUser = JsonConvert.DeserializeObject<Dictionary<string, List<Dictionary<string, Uid>>>>(response.Json.ToStringUtf8());

            if (!claimsUser.ContainsKey("uids"))
                return new List<TUser>();

            var uids =
            claimsUser["uids"].Where(x => x.ContainsKey("user_id"))
                .Select(x => x["user_id"]);

            return await GetUsersById(uids, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Find a user token if it exists.
        /// </summary>
        /// <param name="user">The token owner.</param>
        /// <param name="loginProvider">The login provider for the token.</param>
        /// <param name="name">The name of the token.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user token if it exists.</returns>
        public virtual async Task<TUserToken> FindTokenAsync(TUser user, string loginProvider, string name,
            CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            if (user.Tokens?.Count > 0)
            {
                return user.Tokens
                    .LastOrDefault(x => x.LoginProvider == loginProvider && x.Name == name);
            }

            var uttn = new TUserToken().GetDType();

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars($@"query Q($userId: string) {{
                    tokens(func: type({uttn})) @filter(uid_in(user_id, $userId)) @normalize {{
                        uid
                        user_id {{ user_id: uid }}
                        login_provider: login_provider
                        name: name
                        value: value
                    }}
                }}", new Dictionary<string, string> { { "$userId", user.Id } })
                .ConfigureAwait(false);

            user.Tokens = JsonConvert.DeserializeObject<Dictionary<string, List<TUserToken>>>
                                  (userResp.Json.ToStringUtf8())
                              .First(x => x.Key == "tokens").Value ??
                          new List<TUserToken>();

            return user.Tokens
                .OrderByDescending(x => x.Id)
                .FirstOrDefault(x => x.LoginProvider == loginProvider && x.Name == name);
        }

        public virtual Task SetPasswordHashAsync(TUser user, string passwordHash, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);
            CheckNull(passwordHash, nameof(passwordHash));

            user.PasswordHash = passwordHash;

            return Task.CompletedTask;
        }

        public virtual async Task<string> GetPasswordHashAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            if (!(user.PasswordHash is null))
                return user.PasswordHash;

            var usr = await FindByIdAsync(user.Id, cancellationToken)
                .ConfigureAwait(false);

            return user.PasswordHash = usr.PasswordHash;
        }

        public virtual async Task<bool> HasPasswordAsync(TUser user, CancellationToken cancellationToken)
        {
            var hash = await GetPasswordHashAsync(user, cancellationToken)
                .ConfigureAwait(false);

            return !string.IsNullOrEmpty(hash?.Trim());
        }

        public virtual Task SetSecurityStampAsync(TUser user, string stamp, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);
            CheckNull(stamp, nameof(stamp));

            user.SecurityStamp = stamp;
            return Task.CompletedTask;
        }

        public virtual async Task<string> GetSecurityStampAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            if (!(user.SecurityStamp is null))
                return user.SecurityStamp;

            var usr = await FindByIdAsync(user.Id, cancellationToken)
                .ConfigureAwait(false);

            return user.SecurityStamp = usr.SecurityStamp;
        }

        public virtual async Task<DateTimeOffset?> GetLockoutEndDateAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            if (!(user.LockoutEnd is null))
                return user.LockoutEnd;

            var usr = await FindByIdAsync(user.Id, cancellationToken)
                .ConfigureAwait(false);

            return user.LockoutEnd = usr.LockoutEnd;
        }

        public virtual Task SetLockoutEndDateAsync(TUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            if (user.LockoutEnabled)
                user.LockoutEnd = lockoutEnd;

            return Task.CompletedTask;
        }

        public virtual Task<int> IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            if (user.LockoutEnabled)
                user.AccessFailedCount++;

            return Task.FromResult(user.AccessFailedCount);
        }

        public virtual Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            if (user.LockoutEnabled)
                user.AccessFailedCount = 0;

            return Task.CompletedTask;
        }

        public virtual Task<int> GetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            return Task.FromResult(user.AccessFailedCount);
        }

        public virtual Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            return Task.FromResult(user.LockoutEnabled);
        }

        public virtual Task SetLockoutEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            user.LockoutEnabled = enabled;

            return Task.CompletedTask;
        }

        public virtual Task SetPhoneNumberAsync(TUser user, string phoneNumber, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);
            CheckNull(phoneNumber, nameof(phoneNumber));

            user.PhoneNumber = phoneNumber;

            return Task.CompletedTask;
        }

        public virtual async Task<string> GetPhoneNumberAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            if (!(user.PhoneNumber is null))
                return user.PhoneNumber;

            var usr = await FindByIdAsync(user.Id, cancellationToken)
                .ConfigureAwait(false);

            return user.PhoneNumber = usr.PhoneNumber;
        }

        public virtual Task<bool> GetPhoneNumberConfirmedAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            return Task.FromResult(user.PhoneNumberConfirmed);
        }

        public virtual Task SetPhoneNumberConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            user.PhoneNumberConfirmed = confirmed;

            return Task.CompletedTask;
        }

        public virtual Task SetTwoFactorEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            user.TwoFactorEnabled = enabled;

            return Task.CompletedTask;
        }

        public virtual Task<bool> GetTwoFactorEnabledAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            return Task.FromResult(user.TwoFactorEnabled);
        }

        public virtual async Task SetTokenAsync(TUser user, string loginProvider, string name, string value, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);
            CheckNull(loginProvider, nameof(loginProvider));
            CheckNull(name, nameof(name));
            CheckNull(value, nameof(value));

            await FindTokenAsync(user, loginProvider, name, cancellationToken)
                .ConfigureAwait(false);

            var tokens = user.Tokens.Where(x => x.LoginProvider == loginProvider && x.Name == name).ToArray();

            foreach (var ut in tokens)
            {
                await RemoveUserTokenAsync(ut).ConfigureAwait(false);

                user.Tokens?.Remove(ut);
            }

            var token = new TUserToken
            {
                LoginProvider = loginProvider,
                Name = name,
                Value = value
            };

            user.Tokens ??= new List<TUserToken>();
            user.Tokens.Add(token);

            await UpdateAsync(user, cancellationToken);
        }

        public virtual async Task RemoveTokenAsync(TUser user, string loginProvider, string name,
            CancellationToken cancellationToken)
        {
            var ut = await FindTokenAsync(user, loginProvider, name, cancellationToken)
                .ConfigureAwait(false);

            if (!(ut is null))
            {
                await RemoveUserTokenAsync(ut).ConfigureAwait(false);

                user.Tokens?.Remove(ut);
            }
        }

        public virtual async Task<string> GetTokenAsync(TUser user, string loginProvider, string name, CancellationToken cancellationToken)
        {
            var token = await FindTokenAsync(user, loginProvider, name, cancellationToken)
                .ConfigureAwait(false);

            return token?.Value;
        }

        private const string InternalLoginProvider = "[AspNetUserStore]";
        private const string AuthenticatorKeyTokenName = "AuthenticatorKey";
        private const string RecoveryCodeTokenName = "RecoveryCodes";

        public virtual Task SetAuthenticatorKeyAsync(TUser user, string key, CancellationToken cancellationToken) =>
            SetTokenAsync(user, InternalLoginProvider, AuthenticatorKeyTokenName, key, cancellationToken);

        public virtual Task<string> GetAuthenticatorKeyAsync(TUser user, CancellationToken cancellationToken)
            => GetTokenAsync(user, InternalLoginProvider, AuthenticatorKeyTokenName, cancellationToken);

        public virtual Task ReplaceCodesAsync(TUser user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken)
        {
            var mergedCodes = string.Join(";", recoveryCodes);
            return SetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName, mergedCodes, cancellationToken);
        }

        public virtual async Task<bool> RedeemCodeAsync(TUser user, string code, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);
            CheckNull(code, nameof(code));

            var mergedCodes = await GetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName,
                                  cancellationToken).ConfigureAwait(false) ?? "";
            var splitCodes = mergedCodes.Split(';');

            if (!splitCodes.Contains(code))
                return false;

            var updatedCodes = new List<string>(splitCodes.Where(s => s != code));
            await ReplaceCodesAsync(user, updatedCodes, cancellationToken)
                .ConfigureAwait(false);

            return true;
        }

        public virtual async Task<int> CountCodesAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            var mergedCodes = await GetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName,
                                  cancellationToken).ConfigureAwait(false) ?? "";
            return mergedCodes.Length > 0 ? mergedCodes.Split(';').Length : 0;
        }

        ~UserStore()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            _disposed = true;

            if (disposing)
            {
                Context?.Dispose();
            }
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask(Task.Run(delegate
            {
                Dispose(true);
            }));
        }

        #region Shared
        private static void CheckString(string str, string paramName)
        {
            if (string.IsNullOrWhiteSpace(str))
                throw new ArgumentException("{0} can not be null.", paramName);
        }

        private static void CheckNull<T>(T obj, string paramName)
        {
            if (obj is null)
                throw new ArgumentException("{0} can not be null.", paramName);
        }

        private void CheckUser(TUser user, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed(cancellationToken);
            CheckNull(user, nameof(user));
        }

        private IdentityResult CreateError(Exception exception)
        {
            var e = new IdentityError { Code = exception.Source, Description = exception.Message };
            _logger.LogError(exception, e.Description);

            return IdentityResult.Failed(e);
        }
        #endregion

        #region Update
        /// <summary>
        /// Updates the specified <paramref name="user"/> in the user store.
        /// </summary>
        /// <param name="user">The user to update.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/> of the update operation.</returns>
        public virtual async Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);

            user.ConcurrencyStamp = Guid.NewGuid().ToString();

            var usr = JsonConvert.SerializeObject(user, new JsonSerializerSettings {
                NullValueHandling = NullValueHandling.Include,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });

            var req = new Request { CommitNow = true };
            var mu = new Mutation { CommitNow = true, SetJson = ByteString.CopyFromUtf8(usr) };
            req.Mutations.Add(mu);

            if (user.Id.IsEmpty || user.Id.IsReferenceOnly)
                return IdentityResult.Failed(ErrorDescriber.DuplicateUserName(user.UserName));

            await using var txn = GetTransaction(cancellationToken);
            try
            {
                await txn.Do(req).ConfigureAwait(false);
            }
            catch (RpcException re)
            {
                _logger.LogError(re, re.Message);

                return IdentityResult.Failed(new IdentityError { Code = re.StatusCode.ToString(), Description = re.Status.Detail });
            }
            catch (Exception ex)
            {
                var e = ErrorDescriber.ConcurrencyFailure();
                _logger.LogError(ex, e.Description);

                return IdentityResult.Failed(e);
            }
            return IdentityResult.Success;
        }
        #endregion

        #region Create
        /// <summary>
        /// Creates the specified <paramref name="user"/> in the user store.
        /// </summary>
        /// <param name="user">The user to create.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/> of the creation operation.</returns>
        public virtual async Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);

            var utn = new TUser().GetDType();

            var q = $@"
            query Q($userName: string) {{
                u as var(func: type({utn})) @filter(eq(normalized_username, $userName))
            }}";

            var req = new Request
            {
                CommitNow = true,
                Query = q
            };

            await SetNormalizedEmailAsync(user, user.Email.ToUpperInvariant(), cancellationToken)
                .ConfigureAwait(false);
            req.Vars.Add("$userName", user.NormalizedUserName);

            var mu = new Mutation
            {
                CommitNow = true,
                Cond = "@if(eq(len(u), 0))",
                SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(user))
            };

            req.Mutations.Add(mu);

            await using var txn = GetTransaction(cancellationToken);

            try
            {
                var response = await txn.Do(req).ConfigureAwait(false);

                if (response.Uids.Count == 0)
                    return IdentityResult.Failed(ErrorDescriber.DuplicateUserName(user.UserName));

                user.Id = response.Uids.First().Value;

                return IdentityResult.Success;
            }
            catch (Exception ex)
            {
                var e = ErrorDescriber.DefaultError();
                _logger.LogError(ex, e.Description);

                return IdentityResult.Failed(e);
            }
        }
        #endregion

        #region Delete
        /// <summary>
        /// Deletes the specified <paramref name="user"/> from the user store.
        /// </summary>
        /// <param name="user">The user to delete.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/> of the update operation.</returns>
        public virtual async Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);

            var mu = new Mutation
            {
                CommitNow = true,
                DeleteJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(user))
            };

            await using var txn = GetTransaction(cancellationToken);

            try
            {
                var response = await txn.Mutate(mu).ConfigureAwait(false);
                var resp = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Json.ToStringUtf8());

                if (!resp.TryGetValue("code", out var s) || s?.ToString() != "Success")
                    return IdentityResult.Failed(ErrorDescriber.DefaultError());
            }
            catch (Exception ex)
            {
                return CreateError(ex);
            }

            return IdentityResult.Success;
        }
        #endregion

        /// <summary>
        /// Adds the given <paramref name="normalizedRoleName"/> to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the role to.</param>
        /// <param name="normalizedRoleName">The role to add.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        // ReSharper disable once UnusedMember.Global
        public virtual async Task AddToRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);
            CheckString(normalizedRoleName, nameof(normalizedRoleName));

            var utn = new TUser().GetDType();
            var rtn = new TRole().GetDType();

            var q = $@"
            query Q($userId: string, $roleName: string) {{
                u as var(func: uid($userId)) @filter(type({utn}))
                r as var(func: eq(normalized_rolename, $roleName)) @filter(type({rtn}))
            }}";

            var mu = new Mutation
            {
                CommitNow = true,
                SetNquads = ByteString.CopyFromUtf8("uid(u) <roles> uid(r) ."),
                Cond = "@if(eq(len(u), 1) AND eq(len(r), 1))"
            };

            var req = new Request
            {
                CommitNow = true,
                Query = q
            };

            req.Mutations.Add(mu);
            req.Vars.Add("$userId", user.Id);
            req.Vars.Add("$roleName", normalizedRoleName);

            await using var txn = GetTransaction(cancellationToken);

            try
            {
                var response = await txn.Do(req).ConfigureAwait(false);
                var resp = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Json.ToStringUtf8());

                if (!resp.TryGetValue("code", out var s) || s?.ToString() != "Success")
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "Role '{0}' or User '{1}' not found.", normalizedRoleName, user.Id));

                var roleEntity = await FindRoleAsync(normalizedRoleName, cancellationToken)
                    .ConfigureAwait(false);
                if (!(roleEntity is null))
                    user.Roles?.Add(roleEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                throw;
            }
        }

        /// <summary>
        /// Removes the given <paramref name="normalizedRoleName"/> from the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to remove the role from.</param>
        /// <param name="normalizedRoleName">The role to remove.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        // ReSharper disable once UnusedMember.Global
        public virtual async Task RemoveFromRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);
            CheckString(normalizedRoleName, nameof(normalizedRoleName));

            var utn = new TUser().GetDType();
            var rtn = new TRole().GetDType();

            var q = $@"
            query Q($userId: string, $roleName: string) {{
                u as var(func: uid($userId)) @filter(type({utn}))
                r as var(func: eq(normalized_rolename, $roleName)) @filter(type({rtn}))
            }}";

            var mu = new Mutation
            {
                CommitNow = true,
                DelNquads = ByteString.CopyFromUtf8("uid(u) <roles> uid(r) ."),
                Cond = "@if(eq(len(u), 1) AND eq(len(r), 1))"
            };

            var req = new Request
            {
                CommitNow = true,
                Query = q
            };

            req.Mutations.Add(mu);
            req.Vars.Add("$userId", user.Id);
            req.Vars.Add("$roleName", normalizedRoleName);

            await using var txn = GetTransaction(cancellationToken);

            try
            {
                var response = await txn.Do(req).ConfigureAwait(false);
                var resp = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Json.ToStringUtf8());

                if (!resp.TryGetValue("code", out var s) || s?.ToString() != "Success")
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "Role '{0}' or User '{1}' not found.", normalizedRoleName, user.Id));

                var ur = user.Roles?.FirstOrDefault(x => x.NormalizedName == normalizedRoleName);

                if (!(ur is null))
                    user.Roles?.Remove(ur);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                throw;
            }
        }

        private static Request CreateClaimRequest(TUser user, Claim claim)
        {
            var uctn = new TUserClaim().GetDType();
            var q = $@"
            query Q($userId: string, $claimType: string) {{
                c as var(func: eq(claim_type, $claimType)) @filter(type({uctn}) AND uid_in(user_id, $userId))
            }}";

            var uc = DUserClaim<TUserClaim, TUser>.InitializeFrom(user, claim);

            var mu = new Mutation
            {
                SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(uc)),
                CommitNow = true,
                Cond = "@if(eq(len(c), 0))"
            };

            var req = new Request { Query = q, CommitNow = true };
            req.Mutations.Add(mu);
            req.Vars.Add("$userId", user.Id);
            req.Vars.Add("$claimType", claim.Type);

            return req;
        }

        private static Request RemoveClaimRequest(TUser user, Claim claim)
        {
            var uctn = new TUserClaim().GetDType();

            var q = $@"
            query Q($userId: string, $claimType: string) {{
                c as var(func: eq(claim_type, $claimType)) @filter(type({uctn}) AND uid_in(user_id, $userId))
            }}";

            var mu = new Mutation
            {
                DelNquads = ByteString.CopyFromUtf8("uid(c) * * ."),
                CommitNow = true,
                Cond = "@if(eq(len(c), 1))"
            };

            var req = new Request { Query = q, CommitNow = true };
            req.Mutations.Add(mu);
            req.Vars.Add("$userId", user.Id);
            req.Vars.Add("$claimType", claim.Type);

            return req;
        }

        /// <summary>
        /// Adds the <paramref name="claim"/> given to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the claim to.</param>
        /// <param name="claim">The claim to add to the user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task AddClaimAsync(TUser user, Claim claim, CancellationToken cancellationToken) =>
            AddClaimsAsync(user, new[] { claim }, cancellationToken);

        /// <summary>
        /// Adds the <paramref name="claims"/> given to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the claim to.</param>
        /// <param name="claims">The claim to add to the user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual async Task AddClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);
            claims ??= Array.Empty<Claim>();

            await using var txn = GetTransaction(cancellationToken);

            try
            {
                await txn.Do(claims.Select(claim => CreateClaimRequest(user, claim)))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                throw;
            }
        }

        /// <summary>
        /// Replaces the <paramref name="claim"/> on the specified <paramref name="user"/>, with the <paramref name="newClaim"/>.
        /// </summary>
        /// <param name="user">The user to replace the claim on.</param>
        /// <param name="claim">The claim replace.</param>
        /// <param name="newClaim">The new claim replacing the <paramref name="claim"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task ReplaceClaimAsync(TUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);
            CheckNull(claim, nameof(claim));
            CheckNull(newClaim, nameof(newClaim));

            return Task.WhenAll(RemoveClaimsAsync(user, new[] { claim }, cancellationToken),
                AddClaimAsync(user, newClaim, cancellationToken));
        }

        /// <summary>
        /// Removes the <paramref name="claims"/> given from the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to remove the claims from.</param>
        /// <param name="claims">The claim to remove.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual async Task RemoveClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            CheckUser(user, cancellationToken);
            CheckNull(claims, nameof(claims));

            await using var txn = GetTransaction(cancellationToken);

            try
            {
                await txn.Do(claims.Select(claim => RemoveClaimRequest(user, claim)))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                throw;
            }
        }

        /// <summary>
        /// Called to create a new instance of a <see cref="IdentityUserLogin{TKey}"/>.
        /// </summary>
        /// <param name="user">The associated user.</param>
        /// <param name="login">The sasociated login.</param>
        /// <returns></returns>
        public virtual TUserLogin CreateUserLogin(TUser user, UserLoginInfo login)
        {
            var ul = new TUserLogin
            {
                ProviderKey = login.ProviderKey,
                LoginProvider = login.LoginProvider,
                ProviderDisplayName = login.ProviderDisplayName
            };

            user.Logins.Add(ul);

            return ul;
        }

        /// <summary>
        /// Adds the <paramref name="login"/> given to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the login to.</param>
        /// <param name="login">The login to add to the user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual async Task AddLoginAsync(TUser user, UserLoginInfo login, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);
            CheckNull(login, nameof(login));

            var userLogin = CreateUserLogin(user, login);

            var ultn = new TUserLogin().GetDType();

            var q = $@"
            query Q($userId: string, $providerKey: string, $providerName: string) {{
                p as var(func: eq(provider_key, $providerKey)) @filter(eq(login_provider, $providerName) AND type({ultn}) AND uid_in(user_id, $userId))
            }}";

            var mu = new Mutation
            {
                SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(userLogin)),
                CommitNow = true,
                Cond = "@if(eq(len(p), 0))"
            };

            var req = new Request { Query = q, CommitNow = true };
            req.Mutations.Add(mu);
            req.Vars.Add("$userId", user.Id);
            req.Vars.Add("$providerKey", userLogin.ProviderKey);
            req.Vars.Add("$providerName", userLogin.LoginProvider);

            await using var txn = GetTransaction(cancellationToken);

            try
            {
                await txn.Do(req).ConfigureAwait(false);
                userLogin = await FindUserLoginAsync(user.Id, userLogin.LoginProvider, userLogin.ProviderKey,
                    cancellationToken).ConfigureAwait(false);

                user.Logins?.Add(userLogin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                throw;
            }
        }

        /// <summary>
        /// Removes the <paramref name="loginProvider"/> given from the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to remove the login from.</param>
        /// <param name="loginProvider">The login to remove from the user.</param>
        /// <param name="providerKey">The key provided by the <paramref name="loginProvider"/> to identify a user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual async Task RemoveLoginAsync(TUser user, string loginProvider, string providerKey, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);
            CheckNull(loginProvider, nameof(loginProvider));
            CheckNull(providerKey, nameof(providerKey));

            var ultn = new TUserLogin().GetDType();

            var q = $@"
            query Q($userId: string, $providerKey: string, $providerName: string) {{
                p as var(func: eq(provider_key, $providerKey)) @filter(eq(login_provider, $providerName) AND type({ultn}) AND uid_in(user_id, $userId))
            }}";

            var mu = new Mutation
            {
                DelNquads = ByteString.CopyFromUtf8("uid(p) * * ."),
                CommitNow = true,
                Cond = "@if(eq(len(p), 1))"
            };

            var req = new Request { Query = q, CommitNow = true };
            req.Mutations.Add(mu);
            req.Vars.Add("$userId", user.Id);
            req.Vars.Add("$providerKey", providerKey);
            req.Vars.Add("$providerName", loginProvider);

            await using var txn = GetTransaction(cancellationToken);

            try
            {
                await txn.Do(req).ConfigureAwait(false);

                var userLogin = user.Logins?.FirstOrDefault(x => x.LoginProvider == loginProvider &&
                                                                 x.ProviderKey == providerKey);

                user.Logins?.Remove(userLogin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                throw;
            }
        }

        /// <summary>
        /// Add a new user token.
        /// </summary>
        /// <param name="token">The token to be added.</param>
        /// <returns></returns>
        public virtual async Task AddUserTokenAsync(TUserToken token)
        {
            CheckNull(token, nameof(token));

            var mu = new Mutation
            {
                SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(token)),
                CommitNow = true
            };

            await using var txn = GetTransaction();

            try
            {
                var tkResp = await txn.Mutate(mu).ConfigureAwait(false);
                token.Id = tkResp.Uids.First().Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                throw;
            }
        }

        /// <summary>
        /// Remove a new user token.
        /// </summary>
        /// <param name="token">The token to be removed.</param>
        /// <returns></returns>
        public virtual async Task RemoveUserTokenAsync(TUserToken token)
        {
            CheckNull(token, nameof(token));

            var mu = new Mutation
            {
                CommitNow = true,
                DeleteJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(new
                {
                    uid = token.Id.ToString()
                }))
            };

            var req = new Request { CommitNow = true };
            req.Mutations.Add(mu);

            await using var txn = GetTransaction();

            try
            {
                await txn.Do(req).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                throw;
            }
        }
    }
}
