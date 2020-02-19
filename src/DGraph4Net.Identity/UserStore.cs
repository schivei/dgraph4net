using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
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
            base(context, logger, describer) { }
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
            base(context, logger, describer) { }
    }

    /// <summary>
    /// Represents a new instance of a persistence store for the specified user and role types.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    /// <typeparam name="TRole">The type representing a role.</typeparam>
    public class UserStore<TUser, TRole> : UserStore<TUser, TRole, DUserClaim, DUserRole, DUserLogin, DUserToken, DRoleClaim>
        where TUser : DUser
        where TRole : DRole
    {
        /// <summary>
        /// Constructs a new instance of <see cref="UserStore{TUser, TRole}"/>.
        /// </summary>
        /// <param name="context">The <see cref="DGraph"/>.</param>
        /// <param name="logger"></param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/>.</param>
        public UserStore(DGraph context, ILogger<UserStore> logger, IdentityErrorDescriber describer = null) :
            base(context, logger, describer) { }
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
    public class UserStore<TUser, TRole, TUserClaim, TUserRole, TUserLogin, TUserToken, TRoleClaim> :
        UserStoreBase<TUser, TRole, Uid, TUserClaim, TUserRole, TUserLogin, TUserToken, TRoleClaim>,
        IProtectedUserStore<TUser>
        where TUser : DUser
        where TRole : DRole
        where TUserClaim : DUserClaim, new()
        where TUserRole : DUserRole, new()
        where TUserLogin : DUserLogin, new()
        where TUserToken : DUserToken, new()
        where TRoleClaim : DRoleClaim, new()
    {
        private readonly ILogger<UserStore> _logger;

        /// <summary>
        /// Creates a new instance of the store.
        /// </summary>
        /// <param name="context">The context used to access the store.</param>
        /// <param name="logger"></param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to describe store errors.</param>
        public UserStore(DGraph context, ILogger<UserStore> logger, IdentityErrorDescriber describer = null) :
            this(logger, describer ?? new IdentityErrorDescriber()) =>
            Context = context ?? throw new ArgumentNullException(nameof(context));

        private UserStore(ILogger<UserStore> logger, IdentityErrorDescriber describer) : base(describer) => _logger = logger;

        /// <summary>
        /// Gets the database context for this store.
        /// </summary>
        public virtual DGraph Context { get; }

        /// <summary>Saves the current store.</summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        protected Task SaveChanges(CancellationToken cancellationToken)
        {
            // return AutoSaveChanges ? Context.SaveChangesAsync(cancellationToken) :
            return Task.Run(delegate
            { }, cancellationToken);
        }

        private Txn GetTransaction(CancellationToken cancellationToken = default) =>
            Context.NewTransaction(cancellationToken: cancellationToken);

        /// <summary>
        /// Creates the specified <paramref name="user"/> in the user store.
        /// </summary>
        /// <param name="user">The user to create.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/> of the creation operation.</returns>
        public override async Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            const string q = @"
            query Q($userName: string) {
                u as var(func: eq(username.normalized, $userName))
            }";

            var req = new Request {
                CommitNow = true,
                Query = q
            };
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
                await txn.Do(req);

                return IdentityResult.Success;
            }
            catch (Exception ex)
            {
                var e = ErrorDescriber.DefaultError();
                _logger.LogError(ex, e.Description);

                return IdentityResult.Failed(e);
            }
        }


        /// <summary>
        /// Updates the specified <paramref name="user"/> in the user store.
        /// </summary>
        /// <param name="user">The user to update.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/> of the update operation.</returns>
        public override async Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.ConcurrencyStamp = Guid.NewGuid().ToString();

            const string q = @"
            query Q($id: string) {
                u as var(func: eq(uid, uid($id)))
            }";

            var req = new Request
            {
                CommitNow = true,
                Query = q
            };

            var mu = new Mutation
            {
                CommitNow = true,
                Cond = "@if(eq(len(uid(u), 1)))",
                SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(user))
            };

            req.Mutations.Add(mu);

            var txn = GetTransaction(cancellationToken);

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

        /// <summary>
        /// Deletes the specified <paramref name="user"/> from the user store.
        /// </summary>
        /// <param name="user">The user to delete.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/> of the update operation.</returns>
        public override async Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var mu = new Mutation {CommitNow = true, DelNquads = ByteString.CopyFromUtf8("uid(u) * * .")};

            const string q = @"
            query Q($id: string) {
                u as var(func: eq(uid, uid($id)))
            }";

            var req = new Request
            {
                CommitNow = true,
                Query = q
            };

            req.Mutations.Add(mu);

            var txn = GetTransaction(cancellationToken);
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

        /// <summary>
        /// Finds and returns a user, if any, who has the specified <paramref name="userId"/>.
        /// </summary>
        /// <param name="userId">The user ID to search for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the user matching the specified <paramref name="userId"/> if it exists.
        /// </returns>
        public override async Task<TUser> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            var id = ConvertIdFromString(userId);

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars(@"query Q($userId string) {
                    user(func: eq(uid, uid($userId))) {
                        uid
                        expand(_all_)
                        claims {
                            uid
                            expand(_all_)
                        }
                        roles {
                            uid
                            expand(_all_)
                        }
                        logins {
                            uid
                            expand(_all_)
                        }
                        tokens {
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
        public override async Task<TUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars(@"query Q($userName string) {
                    user(func: eq(username.normalized, $userName)) {
                        uid
                        expand(_all_)
                        claims {
                            uid
                            expand(_all_)
                        }
                        roles {
                            uid
                            expand(_all_)
                        }
                        logins {
                            uid
                            expand(_all_)
                        }
                        tokens {
                            uid
                            expand(_all_)
                        }
                    }
                }", new Dictionary<string, string> { { "$userName", normalizedUserName } });

            return JsonConvert.DeserializeObject<Dictionary<string, List<TUser>>>(userResp.Json.ToStringUtf8())
                .First(x => x.Key == "user").Value?.FirstOrDefault();
        }

        /// <summary>
        /// A navigation property for the users the store contains.
        /// </summary>
        public override IQueryable<TUser> Users => null;

        /// <summary>
        /// Return a role with the normalized name if it exists.
        /// </summary>
        /// <param name="normalizedRoleName">The normalized role name.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The role if it exists.</returns>
        protected override async Task<TRole> FindRoleAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var resp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars(@"query Q($roleName string) {
                    role(func: eq(rolename.normalized, $roleName)) {
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
        protected override async Task<TUserRole> FindUserRoleAsync(Uid userId, Uid roleId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var resp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars(@"query Q($userId string, $roleId string) {
                    userRole(func: uid($userId)) @normalize {
                        userId: uid
                        roles @filter(uid($roleId)) {
                            roleId: uid
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
        protected override async Task<TUser> FindUserAsync(Uid userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars(@"query Q($userId string) {
                    user(func: uid($userId)) {
                        uid
                        expand(_all_)
                        claims {
                            uid
                            expand(_all_)
                        }
                        roles {
                            uid
                            expand(_all_)
                        }
                        logins {
                            uid
                            expand(_all_)
                        }
                        tokens {
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
        protected override async Task<TUserLogin> FindUserLoginAsync(Uid userId, string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars(@"query Q($userId string) {
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

            if(userLogin != null)
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
        protected override async Task<TUserLogin> FindUserLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars(@"query Q($loginProvider string, $providerKey string) {
                    userLogin(func: eq(login.provider, $loginProvider)) @filter(eq(provider.key, $providerKey)) {
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
        /// Adds the given <paramref name="normalizedRoleName"/> to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the role to.</param>
        /// <param name="normalizedRoleName">The role to add.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override async Task AddToRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (string.IsNullOrWhiteSpace(normalizedRoleName))
            {
                throw new ArgumentException("{0} can not be null.", nameof(normalizedRoleName));
            }
            var roleEntity = await FindRoleAsync(normalizedRoleName, cancellationToken);
            if (roleEntity == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "Role '{0}' not found.", normalizedRoleName));
            }

            var userRole = CreateUserRole(user, roleEntity);

            var mu = new Mutation { CommitNow = true,
                SetNquads = ByteString.CopyFromUtf8("uid(u) <roles> uid(r) ."),
                Cond = "@if(eq(len(u), 1) AND eq(len(r), 1))"
            };

            const string q = @"
            query Q($userId: string, $roleId: string) {
                u as var(func: eq(uid, uid($userId)))
                r as var(func: eq(uid, uid($roleId)))
            }";

            var req = new Request
            {
                CommitNow = true,
                Query = q
            };

            req.Mutations.Add(mu);
            req.Vars.Add("$userId", userRole.UserId);
            req.Vars.Add("$roleId", userRole.RoleId);

            var txn = GetTransaction(cancellationToken);

            try
            {
                await txn.Do(req);

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
        public override async Task RemoveFromRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (string.IsNullOrWhiteSpace(normalizedRoleName))
            {
                throw new ArgumentException(Resources.ValueCannotBeNullOrEmpty, nameof(normalizedRoleName));
            }
            var roleEntity = await FindRoleAsync(normalizedRoleName, cancellationToken);
            if (roleEntity != null)
            {
                var userRole = await FindUserRoleAsync(user.Id, roleEntity.Id, cancellationToken);
                if (userRole != null)
                {
                    UserRoles.Remove(userRole);
                }
            }
        }

        /// <summary>
        /// Retrieves the roles the specified <paramref name="user"/> is a member of.
        /// </summary>
        /// <param name="user">The user whose roles should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that contains the roles the user is a member of.</returns>
        public override async Task<IList<string>> GetRolesAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            var userId = user.Id;
            var query = from userRole in UserRoles
                        join role in Roles on userRole.RoleId equals role.Id
                        where userRole.UserId.Equals(userId)
                        select role.Name;
            return await query.ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Returns a flag indicating if the specified user is a member of the give <paramref name="normalizedRoleName"/>.
        /// </summary>
        /// <param name="user">The user whose role membership should be checked.</param>
        /// <param name="normalizedRoleName">The role to check membership of</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> containing a flag indicating if the specified user is a member of the given group. If the
        /// user is a member of the group the returned value with be true, otherwise it will be false.</returns>
        public override async Task<bool> IsInRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (string.IsNullOrWhiteSpace(normalizedRoleName))
            {
                throw new ArgumentException(Resources.ValueCannotBeNullOrEmpty, nameof(normalizedRoleName));
            }
            var role = await FindRoleAsync(normalizedRoleName, cancellationToken);
            if (role != null)
            {
                var userRole = await FindUserRoleAsync(user.Id, role.Id, cancellationToken);
                return userRole != null;
            }
            return false;
        }

        /// <summary>
        /// Get the claims associated with the specified <paramref name="user"/> as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose claims should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that contains the claims granted to a user.</returns>
        public async override Task<IList<Claim>> GetClaimsAsync(TUser user, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return await UserClaims.Where(uc => uc.UserId.Equals(user.Id)).Select(c => c.ToClaim()).ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Adds the <paramref name="claims"/> given to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the claim to.</param>
        /// <param name="claims">The claim to add to the user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override Task AddClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (claims == null)
            {
                throw new ArgumentNullException(nameof(claims));
            }
            foreach (var claim in claims)
            {
                UserClaims.Add(CreateUserClaim(user, claim));
            }
            return Task.FromResult(false);
        }

        /// <summary>
        /// Replaces the <paramref name="claim"/> on the specified <paramref name="user"/>, with the <paramref name="newClaim"/>.
        /// </summary>
        /// <param name="user">The user to replace the claim on.</param>
        /// <param name="claim">The claim replace.</param>
        /// <param name="newClaim">The new claim replacing the <paramref name="claim"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public async override Task ReplaceClaimAsync(TUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (claim == null)
            {
                throw new ArgumentNullException(nameof(claim));
            }
            if (newClaim == null)
            {
                throw new ArgumentNullException(nameof(newClaim));
            }

            var matchedClaims = await UserClaims.Where(uc => uc.UserId.Equals(user.Id) && uc.ClaimValue == claim.Value && uc.ClaimType == claim.Type).ToListAsync(cancellationToken);
            foreach (var matchedClaim in matchedClaims)
            {
                matchedClaim.ClaimValue = newClaim.Value;
                matchedClaim.ClaimType = newClaim.Type;
            }
        }

        /// <summary>
        /// Removes the <paramref name="claims"/> given from the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to remove the claims from.</param>
        /// <param name="claims">The claim to remove.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public async override Task RemoveClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (claims == null)
            {
                throw new ArgumentNullException(nameof(claims));
            }
            foreach (var claim in claims)
            {
                var matchedClaims = await UserClaims.Where(uc => uc.UserId.Equals(user.Id) && uc.ClaimValue == claim.Value && uc.ClaimType == claim.Type).ToListAsync(cancellationToken);
                foreach (var c in matchedClaims)
                {
                    UserClaims.Remove(c);
                }
            }
        }

        /// <summary>
        /// Adds the <paramref name="login"/> given to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the login to.</param>
        /// <param name="login">The login to add to the user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override Task AddLoginAsync(TUser user, UserLoginInfo login,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (login == null)
            {
                throw new ArgumentNullException(nameof(login));
            }
            UserLogins.Add(CreateUserLogin(user, login));
            return Task.FromResult(false);
        }

        /// <summary>
        /// Removes the <paramref name="loginProvider"/> given from the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to remove the login from.</param>
        /// <param name="loginProvider">The login to remove from the user.</param>
        /// <param name="providerKey">The key provided by the <paramref name="loginProvider"/> to identify a user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override async Task RemoveLoginAsync(TUser user, string loginProvider, string providerKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            var entry = await FindUserLoginAsync(user.Id, loginProvider, providerKey, cancellationToken);
            if (entry != null)
            {
                UserLogins.Remove(entry);
            }
        }

        /// <summary>
        /// Retrieves the associated logins for the specified <param ref="user"/>.
        /// </summary>
        /// <param name="user">The user whose associated logins to retrieve.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> for the asynchronous operation, containing a list of <see cref="UserLoginInfo"/> for the specified <paramref name="user"/>, if any.
        /// </returns>
        public async override Task<IList<UserLoginInfo>> GetLoginsAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            var userId = user.Id;
            return await UserLogins.Where(l => l.UserId.Equals(userId))
                .Select(l => new UserLoginInfo(l.LoginProvider, l.ProviderKey, l.ProviderDisplayName)).ToListAsync(cancellationToken);
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
        public async override Task<TUser> FindByLoginAsync(string loginProvider, string providerKey,
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

        /// <summary>
        /// Gets the user, if any, associated with the specified, normalized email address.
        /// </summary>
        /// <param name="normalizedEmail">The normalized email address to return the user for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The task object containing the results of the asynchronous lookup operation, the user if any associated with the specified normalized email address.
        /// </returns>
        public override Task<TUser> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            return Task.FromResult(Users.Where(u => u.NormalizedEmail == normalizedEmail).SingleOrDefault());
        }

        /// <summary>
        /// Retrieves all users with the specified claim.
        /// </summary>
        /// <param name="claim">The claim whose users should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> contains a list of users, if any, that contain the specified claim.
        /// </returns>
        public async override Task<IList<TUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (claim == null)
            {
                throw new ArgumentNullException(nameof(claim));
            }

            var query = from userclaims in UserClaims
                        join user in Users on userclaims.UserId equals user.Id
                        where userclaims.ClaimValue == claim.Value
                        && userclaims.ClaimType == claim.Type
                        select user;

            return await query.ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Retrieves all users in the specified role.
        /// </summary>
        /// <param name="normalizedRoleName">The role whose users should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> contains a list of users, if any, that are in the specified role.
        /// </returns>
        public async override Task<IList<TUser>> GetUsersInRoleAsync(string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(normalizedRoleName))
            {
                throw new ArgumentNullException(nameof(normalizedRoleName));
            }

            var role = await FindRoleAsync(normalizedRoleName, cancellationToken);

            if (role != null)
            {
                var query = from userrole in UserRoles
                            join user in Users on userrole.UserId equals user.Id
                            where userrole.RoleId.Equals(role.Id)
                            select user;

                return await query.ToListAsync(cancellationToken);
            }
            return new List<TUser>();
        }

        /// <summary>
        /// Find a user token if it exists.
        /// </summary>
        /// <param name="user">The token owner.</param>
        /// <param name="loginProvider">The login provider for the token.</param>
        /// <param name="name">The name of the token.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user token if it exists.</returns>
        protected override Task<TUserToken> FindTokenAsync(TUser user, string loginProvider, string name, CancellationToken cancellationToken)
            => UserTokens.FindAsync(new object[] { user.Id, loginProvider, name }, cancellationToken).AsTask();

        /// <summary>
        /// Add a new user token.
        /// </summary>
        /// <param name="token">The token to be added.</param>
        /// <returns></returns>
        protected override Task AddUserTokenAsync(TUserToken token)
        {
            UserTokens.Add(token);
            return Task.CompletedTask;
        }


        /// <summary>
        /// Remove a new user token.
        /// </summary>
        /// <param name="token">The token to be removed.</param>
        /// <returns></returns>
        protected override Task RemoveUserTokenAsync(TUserToken token)
        {
            UserTokens.Remove(token);
            return Task.CompletedTask;
        }
    }
}
}
