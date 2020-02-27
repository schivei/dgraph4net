using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DGraph4Net.Services;
using Google.Protobuf;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DGraph4Net.Identity
{
    /// <summary>
    /// Represents a new instance of a persistence store for users, using the default implementation
    /// of <see cref="IdentityUser{Uid}"/> with a string as a primary key.
    /// </summary>
    public class UserStore : UserStore<DUser>
    {
        /// <summary>
        /// Constructs a new instance of <see cref="UserStore"/>.
        /// </summary>
        /// <param name="context">The <see cref="DGraph"/>.</param>
        /// <param name="logger"></param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/>.</param>
        public UserStore(DGraph context, ILogger<UserStore> logger, IdentityErrorDescriber describer = null) :
            base(context, logger, describer)
        { }
    }

    /// <summary>
    /// Creates a new instance of a persistence store for the specified user type.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    public class UserStore<TUser> : UserStore<TUser, DRole>
        where TUser : DUser, new()
    {
        /// <summary>
        /// Constructs a new instance of <see cref="UserStore{TUser}"/>.
        /// </summary>
        /// <param name="context">The <see cref="DGraph"/>.</param>
        /// <param name="logger"></param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/>.</param>
        public UserStore(DGraph context, ILogger<UserStore> logger, IdentityErrorDescriber describer = null) :
            base(context, logger, describer)
        { }
    }

    /// <summary>
    /// Represents a new instance of a persistence store for the specified user and role types.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    /// <typeparam name="TRole">The type representing a role.</typeparam>
    public class UserStore<TUser, TRole> : UserStore<TUser, TRole, DUserClaim, DUserRole, DUserLogin, DUserToken, DRoleClaim>
        where TUser : DUser, new()
        where TRole : DRole, new()
    {
        /// <summary>
        /// Constructs a new instance of <see cref="UserStore{TUser, TRole}"/>.
        /// </summary>
        /// <param name="context">The <see cref="DGraph"/>.</param>
        /// <param name="logger"></param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/>.</param>
        public UserStore(DGraph context, ILogger<UserStore> logger, IdentityErrorDescriber describer = null) :
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
        IAsyncDisposable
        where TUser : DUser, new()
        where TRole : DRole, new()
        where TUserClaim : DUserClaim, new()
        where TUserRole : DUserRole, new()
        where TUserLogin : DUserLogin, new()
        where TUserToken : DUserToken, new()
        where TRoleClaim : DRoleClaim, new()
    {
        private readonly ILogger<UserStore> _logger;
        private bool _disposed;

        /// <summary>
        /// Gets the database context for this store.
        /// </summary>
        public virtual DGraph Context { get; }

        private static IdentityTypeNameOptions<TUser, TRole, TUserToken, TUserClaim, TUserLogin, TRoleClaim>
            IdentityTypeNameOptions => new IdentityTypeNameOptions<TUser, TRole, TUserToken, TUserClaim, TUserLogin, TRoleClaim>();

        private Txn GetTransaction(CancellationToken cancellationToken = default) =>
            Context.NewTransaction(cancellationToken: cancellationToken);

        /// <summary>
        /// Creates a new instance of the store.
        /// </summary>
        /// <param name="context">The context used to access the store.</param>
        /// <param name="logger"></param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to describe store errors.</param>
        public UserStore(DGraph context, ILogger<UserStore> logger, IdentityErrorDescriber describer = null) :
            this(logger, describer ?? new IdentityErrorDescriber()) =>
            Context = context ?? throw new ArgumentNullException(nameof(context));

        private UserStore(IdentityErrorDescriber describer) =>
            ErrorDescriber = describer;

        public IdentityErrorDescriber ErrorDescriber { get; }

        private UserStore(ILogger<UserStore> logger, IdentityErrorDescriber describer) : this(describer) => _logger = logger;

        private void ThrowIfDisposed()
        {
            if(_disposed)
                throw new ObjectDisposedException(nameof(UserStore));
        }

        /// <summary>
        /// Throws if this class or context has been disposed.
        /// </summary>
        /// <param name="cancellationToken"></param>
        protected void ThrowIfDisposed(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Context?.Disposed == true)
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

        public virtual Task SetUserNameAsync(TUser user, string userName, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            user.UserName = userName;
            return Task.CompletedTask;
        }

        public virtual Task<string> GetNormalizedUserNameAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            return Task.FromResult(user.NormalizedUserName);
        }

        public virtual Task SetNormalizedUserNameAsync(TUser user, string normalizedName, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
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
                }", new Dictionary<string, string> { { "$userId", id } });

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
                }", new Dictionary<string, string> { { "$userName", normalizedUserName } });

            return JsonConvert.DeserializeObject<Dictionary<string, List<TUser>>>(userResp.Json.ToStringUtf8())
                .First(x => x.Key == "user").Value?.FirstOrDefault();
        }

        /// <summary>
        /// Return a role with the normalized name if it exists.
        /// </summary>
        /// <param name="normalizedRoleName">The normalized role name.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The role if it exists.</returns>
        protected virtual async Task<TRole> FindRoleAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var resp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars(@"query Q($roleName: string) {
                    role(func: eq(normalized_rolename, $roleName)) {
                        uid
                        expand(_all_)
                    }
                }", new Dictionary<string, string> { { "$roleName", normalizedRoleName } });

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
        protected virtual async Task<TUserRole> FindUserRoleAsync(Uid userId, Uid roleId, CancellationToken cancellationToken)
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
            });

            return JsonConvert.DeserializeObject<Dictionary<string, List<TUserRole>>>(resp.Json.ToStringUtf8())
                .First(x => x.Key == "userRole").Value?.FirstOrDefault();
        }

        /// <summary>
        /// Return a user with the matching userId if it exists.
        /// </summary>
        /// <param name="userId">The user's id.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user if it exists.</returns>
        protected virtual async Task<TUser> FindUserAsync(Uid userId, CancellationToken cancellationToken)
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
                }", new Dictionary<string, string> { { "$userId", userId } });

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
        protected virtual async Task<TUserLogin> FindUserLoginAsync(Uid userId, string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars(@"query Q($userId: string) {
                    user(func: uid($userId)) {
                        logins {
                            uid
                            expand(_all_)
                        }
                    }
                }", new Dictionary<string, string> { { "$userId", userId } });

            var userLogin = JsonConvert.DeserializeObject<Dictionary<string, List<TUser>>>(userResp.Json.ToStringUtf8())
                .First(x => x.Key == "user").Value?.FirstOrDefault()?
                .Logins.FirstOrDefault(ul => ul.LoginProvider == loginProvider &&
                                                      ul.ProviderKey == providerKey);

            if (userLogin != null)
                userLogin.UserId = userId;

            return userLogin as TUserLogin;
        }

        /// <summary>
        /// Return a user login with  provider, providerKey if it exists.
        /// </summary>
        /// <param name="loginProvider">The login provider name.</param>
        /// <param name="providerKey">The key provided by the <paramref name="loginProvider"/> to identify a user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user login if it exists.</returns>
        protected virtual async Task<TUserLogin> FindUserLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
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
                });

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
            var userRoles = await GetUserRolesAsync(user, cancellationToken);
            
            return userRoles.Select(x => x.NormalizedName).ToList();
        }

        public virtual async Task<IList<TRole>> GetUserRolesAsync(TUser user,
            CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);

            if (!(user.Roles is null))
                return user.Roles.Select(DRole.Initialize<TRole>).ToList();

            var usr = await FindByIdAsync(user.Id, cancellationToken);
            user.Populate(usr);

            return user.Roles?.Select(DRole.Initialize<TRole>).ToList() ?? new List<TRole>();
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

            var userRoles = await GetRolesAsync(user, cancellationToken);

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
                return user.Claims.Select(c => c.ToClaim()).ToList();

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars($@"query Q($userId: string) {{
                    claims(func: eq(dgraph.type, ""{IdentityTypeNameOptions.UserClaimTypeName}"")) @filter(uid_in(user_id, $userId)) {{
                        uid
                        expand(_all_)
                    }}
                }}", new Dictionary<string, string> { { "$userId", user.Id } });

            user.Claims = JsonConvert.DeserializeObject<Dictionary<string, List<DUserClaim>>>
                    (userResp.Json.ToStringUtf8())
                .First(x => x.Key == "claims").Value ?? new List<DUserClaim>();

            return user.Claims.Select(c => c.ToClaim()).ToList();
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

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars($@"query Q($userId: string) {{
                    logins(func: eq(dgraph.type, ""{IdentityTypeNameOptions.UserLoginTypeName}"")) @filter(uid_in(user_id, $userId)) {{
                        uid
                        expand(_all_)
                    }}
                }}", new Dictionary<string, string> { { "$userId", user.Id } });

            user.Logins = JsonConvert.DeserializeObject<Dictionary<string, List<DUserLogin>>>
                                  (userResp.Json.ToStringUtf8())
                              .First(x => x.Key == "logins").Value ??
                          new List<DUserLogin>();

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
            var userLogin = await FindUserLoginAsync(loginProvider, providerKey, cancellationToken);
            if (userLogin != null)
            {
                return await FindUserAsync(userLogin.UserId, cancellationToken);
            }
            return null;
        }

        public virtual async Task SetEmailAsync(TUser user, string email, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            var usr = await FindByEmailAsync(email.ToUpperInvariant(), cancellationToken);

            if (usr != null && usr != user)
                throw new AmbiguousMatchException("Email already exists.");

            user.Email = email;
            
        }

        public virtual async Task<string> GetEmailAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            if (!(user.Email is null))
                return user.Email;

            var usr = await FindByIdAsync(user.Id, cancellationToken);
            user.Populate(usr);

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

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars(@"query Q($userEmail: string) {
                    user(func: eq(normalized_email, $userEmail)) {
                        uid
                        expand(_all_)
                        roles {
                              uid
                              expand(_all_)
                        }
                    }
                }", new Dictionary<string, string> { { "$userEmail", normalizedEmail } });

            return JsonConvert.DeserializeObject<Dictionary<string, List<TUser>>>(userResp.Json.ToStringUtf8())
                .First(x => x.Key == "user").Value?.FirstOrDefault();
        }

        public virtual async Task<string> GetNormalizedEmailAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            if (!(user.NormalizedUserName is null))
                return user.NormalizedUserName;

            var usr = await FindByIdAsync(user.Id, cancellationToken);
            user.Populate(usr);

            return user.NormalizedUserName;
        }

        public virtual async Task SetNormalizedEmailAsync(TUser user, string normalizedEmail, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            var usr = await FindByEmailAsync(normalizedEmail, cancellationToken);

            if (usr != null && usr != user)
                throw new AmbiguousMatchException("Email already exists.");

            user.NormalizedEmail = normalizedEmail;
        }

        private async Task<IList<TUser>> GetUsersById(IEnumerable<Uid> uids, CancellationToken cancellationToken)
        {
            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .Query($@"{{
                    users(func: uid({string.Join(',', uids)})) {{
                        uid
                        expand(_all_)
                        roles {{
                              uid
                              expand(_all_)
                        }}
                    }}
                }}");

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

            var q = $@"
            query Q($claimType: string, $claimValue: string) {{
                claims(func: eq(claim_type, $claimType)) @filter(eq(dgraph.type, ""{IdentityTypeNameOptions.UserClaimTypeName}"") AND eq(claim_value, $claimValue))) @normalize {{
                    user_id {{
                        user_id: uid
                    }}
                }}
            }}";

            var response = await GetTransaction(cancellationToken).QueryWithVars(q,
                new Dictionary<string, string> {{"$claimType", claim.Type}, {"$claimValue", claim.Value}});

            var claimsUser = JsonConvert.DeserializeObject<Dictionary<string, List<Dictionary<string, Uid>>>>(response.Json.ToStringUtf8());

            if (!claimsUser.ContainsKey("claims"))
                return new List<TUser>();

            var uids =
            claimsUser["claims"].Where(x => x.ContainsKey("user_id"))
                .Select(x => x["user_id"]);

            return await GetUsersById(uids, cancellationToken);
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

            var q = $@"
            query Q($normalizedRoleName: string) {{
                uids(func: eq(normalized_rolename, $normalizedRoleName)) @filter(eq(dgraph.type, ""{IdentityTypeNameOptions.RoleTypeName}"")) @normalize {{
                    ~roles {{
                        user_id: uid
                    }}
                }}
            }}";

            var response = await GetTransaction(cancellationToken).QueryWithVars(q,
                new Dictionary<string, string> { { "$normalizedRoleName", normalizedRoleName } });

            var claimsUser = JsonConvert.DeserializeObject<Dictionary<string, List<Dictionary<string, Uid>>>>(response.Json.ToStringUtf8());

            if (!claimsUser.ContainsKey("uids"))
                return new List<TUser>();

            var uids =
            claimsUser["uids"].Where(x => x.ContainsKey("user_id"))
                .Select(x => x["user_id"]);

            return await GetUsersById(uids, cancellationToken);
        }

        /// <summary>
        /// Find a user token if it exists.
        /// </summary>
        /// <param name="user">The token owner.</param>
        /// <param name="loginProvider">The login provider for the token.</param>
        /// <param name="name">The name of the token.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user token if it exists.</returns>
        protected virtual async Task<TUserToken> FindTokenAsync(TUser user, string loginProvider, string name,
            CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            DUserToken token;
            if (user.Tokens?.Count > 0)
            {
                token = user.Tokens
                    .FirstOrDefault(x => x.LoginProvider == loginProvider && x.Name == name);

                return token is null ? null : DUserToken.Initialize<TUserToken>(token);
            }

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars($@"query Q($userId: string) {{
                    tokens(func: eq(dgraph.type, ""{IdentityTypeNameOptions.UserClaimTypeName}"")) @filter(uid_in(user_id, $userId)) {{
                        uid
                        expand(_all_)
                    }}
                }}", new Dictionary<string, string> { { "$userId", user.Id } });

            user.Tokens = JsonConvert.DeserializeObject<Dictionary<string, List<DUserToken>>>
                                  (userResp.Json.ToStringUtf8())
                              .First(x => x.Key == "tokens").Value ??
                          new List<DUserToken>();

            token = user.Tokens.FirstOrDefault(x => x.LoginProvider == loginProvider && x.Name == name);

            return token is null ? null : DUserToken.Initialize<TUserToken>(token);
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

            if (!(user.PasswordHash is null)) return user.PasswordHash;

            var usr = await FindByIdAsync(user.Id, cancellationToken);
            user.Populate(usr);

            return user.PasswordHash;
        }

        public virtual async Task<bool> HasPasswordAsync(TUser user, CancellationToken cancellationToken)
        {
            var hash = await GetPasswordHashAsync(user, cancellationToken);

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

            var usr = await FindByIdAsync(user.Id, cancellationToken);
            user.Populate(usr);

            return user.SecurityStamp;
        }

        public virtual async Task<DateTimeOffset?> GetLockoutEndDateAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            if (!(user.LockoutEnd is null))
                return user.LockoutEnd;

            var usr = await FindByIdAsync(user.Id, cancellationToken);
            user.Populate(usr);

            return user.LockoutEnd;
        }

        public virtual Task SetLockoutEndDateAsync(TUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            if(user.LockoutEnabled)
                user.LockoutEnd = lockoutEnd;

            return Task.CompletedTask;
        }

        public virtual Task<int> IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            if(user.LockoutEnabled)
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

            var usr = await FindByIdAsync(user.Id, cancellationToken);
            user.Populate(usr);

            return user.PhoneNumber;
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

        public virtual Task SetTokenAsync(TUser user, string loginProvider, string name, string value, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);
            CheckNull(loginProvider, nameof(loginProvider));
            CheckNull(name, nameof(name));
            CheckNull(value, nameof(value));

            var token = new TUserToken
            {
                LoginProvider = loginProvider,
                Name = name,
                UserId = user.Id,
                Value = value
            };

            return AddUserTokenAsync(token);
        }

        public virtual async Task RemoveTokenAsync(TUser user, string loginProvider, string name, CancellationToken cancellationToken)
        {
            var token = await FindTokenAsync(user, loginProvider, name, cancellationToken);

            await RemoveUserTokenAsync(token);
        }

        public virtual async Task<string> GetTokenAsync(TUser user, string loginProvider, string name, CancellationToken cancellationToken)
        {
            var token = await FindTokenAsync(user, loginProvider, name, cancellationToken);

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

            var mergedCodes = await GetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName, cancellationToken) ?? "";
            var splitCodes = mergedCodes.Split(';');

            if (!splitCodes.Contains(code)) return false;

            var updatedCodes = new List<string>(splitCodes.Where(s => s != code));
            await ReplaceCodesAsync(user, updatedCodes, cancellationToken);

            return true;
        }

        public virtual async Task<int> CountCodesAsync(TUser user, CancellationToken cancellationToken)
        {
            CheckUser(user, cancellationToken);

            var mergedCodes = await GetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName, cancellationToken) ?? "";
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
            if (_disposed) return;

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

            var req = new Request { CommitNow = true, Query = $@"query Q($id: string) {{ u as var(func: uid($id))  @filter(eq(dgraph.type, ""{IdentityTypeNameOptions.UserTypeName}"")) }}" };
            var mu = new Mutation { CommitNow = true, Cond = "@if(eq(len(u), 1))", SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(user)) };
            req.Mutations.Add(mu);
            req.Vars.Add("$id", user.Id);
            await using var txn = GetTransaction(cancellationToken);
            try
            {
                await txn.Do(req);
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

            var q = $@"
            query Q($userName: string) {{
                u as var(func: eq(dgraph.type, ""{IdentityTypeNameOptions.UserTypeName}"")) @filter(eq(normalized_username, $userName))
            }}";

            var req = new Request
            {
                CommitNow = true,
                Query = q
            };

            await SetNormalizedEmailAsync(user, user.Email.ToUpperInvariant(), cancellationToken);
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
                var response = await txn.Do(req);

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
                var response = await txn.Mutate(mu);
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

            var q = $@"
            query Q($userId: string, $roleName: string) {{
                u as var(func: uid($userId)) @filter(eq(dgraph.type, ""{IdentityTypeNameOptions.UserTypeName}""))
                r as var(func: eq(normalized_rolename, $roleName)) @filter(eq(dgraph.type, ""{IdentityTypeNameOptions.RoleTypeName}""))
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
                var response = await txn.Do(req);
                var resp = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Json.ToStringUtf8());

                if (!resp.TryGetValue("code", out var s) || s?.ToString() != "Success")
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "Role '{0}' or User '{1}' not found.", normalizedRoleName, user.Id));

                var roleEntity = await FindRoleAsync(normalizedRoleName, cancellationToken);
                if (roleEntity != null)
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

            var q = $@"
            query Q($userId: string, $roleName: string) {{
                u as var(func: uid($userId)) @filter(eq(dgraph.type, ""{IdentityTypeNameOptions.UserTypeName}""))
                r as var(func: eq(normalized_rolename, $roleName)) @filter(eq(dgraph.type, ""{IdentityTypeNameOptions.RoleTypeName}""))
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
                var response = await txn.Do(req);
                var resp = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Json.ToStringUtf8());

                if (!resp.TryGetValue("code", out var s) || s?.ToString() != "Success")
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "Role '{0}' or User '{1}' not found.", normalizedRoleName, user.Id));

                var ur = user.Roles?.FirstOrDefault(x => x.NormalizedName == normalizedRoleName);

                if (ur != null)
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
            var q = $@"
            query Q($userId: string, $claimType: string) {{
                c as var(func: eq(claim_type, $claimType)) @filter(eq(dgraph.type, ""{IdentityTypeNameOptions.UserClaimTypeName}"") AND uid_in(user_id, $userId))
            }}";

            var uc = DUserClaim.InitializeFrom<TUserClaim>(user, claim);

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
            var q = $@"
            query Q($userId: string, $claimType: string) {{
                c as var(func: eq(claim_type, $claimType)) @filter(eq(dgraph.type, ""{IdentityTypeNameOptions.UserClaimTypeName}"") AND uid_in(user_id, $userId))
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
        public Task AddClaimAsync(TUser user, Claim claim, CancellationToken cancellationToken) =>
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
                await txn.Do(claims.Select(claim => CreateClaimRequest(user, claim)));
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
        public virtual async Task ReplaceClaimAsync(TUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);
            CheckNull(claim, nameof(claim));
            CheckNull(newClaim, nameof(newClaim));

            await Task.WhenAll(RemoveClaimsAsync(user, new[] { claim }, cancellationToken), AddClaimAsync(user, newClaim, cancellationToken));
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
                await txn.Do(claims.Select(claim => RemoveClaimRequest(user, claim)));
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
        protected virtual TUserLogin CreateUserLogin(TUser user, UserLoginInfo login)
        {
            return new TUserLogin
            {
                UserId = user.Id,
                ProviderKey = login.ProviderKey,
                LoginProvider = login.LoginProvider,
                ProviderDisplayName = login.ProviderDisplayName
            };
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

            var q = $@"
            query Q($userId: string, $providerKey: string, $providerName: string) {{
                p as var(func: eq(provider_key, $providerKey)) @filter(eq(login_provider, $providerName) AND eq(dgraph.type, ""{IdentityTypeNameOptions.UserLoginTypeName}"") AND uid_in(user_id, $userId))
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
                await txn.Do(req);
                userLogin = await FindUserLoginAsync(user.Id, userLogin.LoginProvider, userLogin.ProviderKey, cancellationToken);

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

            var q = $@"
            query Q($userId: string, $providerKey: string, $providerName: string) {{
                p as var(func: eq(provider_key, $providerKey)) @filter(eq(login_provider, $providerName) AND eq(dgraph.type, ""{IdentityTypeNameOptions.UserLoginTypeName}"") AND uid_in(user_id, $userId))
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
                await txn.Do(req);

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
        protected virtual async Task AddUserTokenAsync(TUserToken token)
        {
            CheckNull(token, nameof(token));

            var q = $@"
            query Q($userId: string, $loginProvider: string, $name: string) {{
                p as var(func: eq(name, $name)) @filter(eq(login_provider, $loginProvider) AND eq(dgraph.type, ""{IdentityTypeNameOptions.UserTokenTypeName}"") AND uid_in(user_id, $userId))
            }}";

            var nq = $@"
            _:ut <user_id> <{token.UserId}> .
            _:ut <dgraph.type> ""UserToken"" .
            _:ut <name> {JsonConvert.SerializeObject(token.Name)} .
            _:ut <login_provider> {JsonConvert.SerializeObject(token.LoginProvider)} .
            _:ut <value> {JsonConvert.SerializeObject(token.Value)} .
            ";

            var mu = new Mutation
            {
                SetNquads = ByteString.CopyFromUtf8(nq),
                CommitNow = true,
                Cond = "@if(eq(len(p), 0))"
            };

            var req = new Request { Query = q, CommitNow = true };
            req.Mutations.Add(mu);
            req.Vars.Add("$userId", token.UserId);
            req.Vars.Add("$loginProvider", token.LoginProvider);
            req.Vars.Add("$name", token.Name);

            await using var txn = GetTransaction();

            try
            {
                await txn.Do(req);
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
        protected virtual async Task RemoveUserTokenAsync(TUserToken token)
        {
            CheckNull(token, nameof(token));

            var q = $@"
            query Q($userId: string, $loginProvider: string, $name: string) {{
                p as var(func: eq(name, $name)) @filter(eq(login_provider, $loginProvider) AND eq(dgraph.type, ""{IdentityTypeNameOptions.UserTokenTypeName}"") AND uid_in(user_id, $userId))
            }}";

            var mu = new Mutation
            {
                DelNquads = ByteString.CopyFromUtf8("uid(p) * * ."),
                CommitNow = true,
                Cond = "@if(eq(len(p), 1))"
            };

            var req = new Request { Query = q, CommitNow = true };
            req.Mutations.Add(mu);
            req.Vars.Add("$userId", token.Id);
            req.Vars.Add("$loginProvider", token.LoginProvider);
            req.Vars.Add("$name", token.Name);

            await using var txn = GetTransaction();

            try
            {
                await txn.Do(req);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                throw;
            }
        }
    }
}
