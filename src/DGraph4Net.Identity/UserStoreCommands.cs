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
    public partial class UserStore<TUser, TRole, TUserClaim, TUserRole, TUserLogin, TUserToken, TRoleClaim>
    {
        #region Obsolete || Backwards compatibility
        /// <summary>Saves the current store.</summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <remarks>Backwards compatibility, this method don't do anything.</remarks>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        protected Task SaveChanges(CancellationToken cancellationToken)
        {
            return Task.Run(delegate
            { }, cancellationToken);
        }
        #endregion

        #region Shared
        private void CheckString(string str, string paramName)
        {
            if (string.IsNullOrWhiteSpace(str))
                throw new ArgumentException("{0} can not be null.", paramName);
        }

        private void CheckNull<T>(T obj, string paramName)
        {
            if (obj is null)
                throw new ArgumentException("{0} can not be null.", paramName);
        }

        private void CheckRole(TRole role, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed(cancellationToken);
            CheckNull(role, nameof(role));
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
        public override async Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);

            user.ConcurrencyStamp = Guid.NewGuid().ToString();

            var req = new Request { CommitNow = true, Query = "query Q($id: string) { u as var(func: eq(uid, uid($id))) }" };
            var mu = new Mutation { CommitNow = true, Cond = "@if(eq(len(uid(u), 1)))", SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(user)) };
            req.Mutations.Add(mu);
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
        public override async Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);

            const string q = @"
            query Q($userName: string) {
                u as var(func: eq(username.normalized, $userName))
            }";

            var req = new Request
            {
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
        #endregion

        #region Delete
        /// <summary>
        /// Deletes the specified <paramref name="user"/> from the user store.
        /// </summary>
        /// <param name="user">The user to delete.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/> of the update operation.</returns>
        public override async Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);

            var mu = new Mutation { CommitNow = true, DelNquads = ByteString.CopyFromUtf8("uid(u) * * .") };

            const string q = @"
            query Q($userId: string) {
                u as var(func: uid($userId))
            }";

            var req = new Request
            {
                CommitNow = true,
                Query = q
            };

            req.Mutations.Add(mu);
            req.Vars.Add("$userId", user.Id);

           await using var txn = GetTransaction(cancellationToken);

            try
            {
                await txn.Do(req);
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
        public override async Task AddToRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);
            CheckString(normalizedRoleName, nameof(normalizedRoleName));

            var roleEntity = await FindRoleAsync(normalizedRoleName, cancellationToken);
            if (roleEntity == null)
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "Role '{0}' not found.", normalizedRoleName));

            var mu = new Mutation
            {
                CommitNow = true,
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
            req.Vars.Add("$userId", user.Id);
            req.Vars.Add("$roleId", roleEntity.Id);

            await using var txn = GetTransaction(cancellationToken);

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
            CheckUser(user, cancellationToken);
            CheckString(normalizedRoleName, nameof(normalizedRoleName));

            var roleEntity = await FindRoleAsync(normalizedRoleName, cancellationToken);
            if (roleEntity == null)
                return;

            var userRole = await FindUserRoleAsync(user.Id, roleEntity.Id, cancellationToken);
            if (userRole is null)
                return;

            var mu = new Mutation
            {
                CommitNow = true,
                DelNquads = ByteString.CopyFromUtf8("uid(u) <roles> uid(r) ."),
                Cond = "@if(eq(len(u), 1) AND eq(len(r), 1))"
            };

            const string q = @"
            query Q($userId: string, $roleId: string) {
                var(func: uid($userId)) {
                    u as uid
                    roles @filter(uid($roleId)) {
                        r as uid
                    }
                }
            }";

            var req = new Request
            {
                CommitNow = true,
                Query = q
            };

            req.Mutations.Add(mu);
            req.Vars.Add("$userId", user.Id);
            req.Vars.Add("$roleId", roleEntity.Id);

           await using var txn = GetTransaction(cancellationToken);

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

        private Request CreateClaimRequest(TUser user, Claim claim)
        {
            const string q = @"
            query Q($user: string, $claimType: string, $claimValue) {
                u as var(func: uid($user))
                c as var(func: eq(claim.value, $claimValue)) @filter(eq(claim.type, $claimType))
            }";

            var mu1 = new Mutation
            {
                SetNquads = ByteString.CopyFromUtf8(@"
                    uid(c) <claim.type> $claimType .
                    uid(c) <claim.value> $claimValue .
                    uid(c) <dgraph.type> Claim .
                "),
                CommitNow = true,
                Cond = "@if(eq(len(u), 1) AND eq(len(c), 0))"
            };

            var mu2 = new Mutation
            {
                SetNquads = ByteString.CopyFromUtf8("uid(u) <claims> uid(c) ."),
                CommitNow = false,
                Cond = "@if(eq(len(u), 1))"
            };

            var req = new Request { Query = q, CommitNow = true };
            req.Mutations.AddRange(new[] { mu1, mu2 });
            req.Vars.Add("$user", user.Id);
            req.Vars.Add("$claimType", claim.Type);
            req.Vars.Add("$claimValue", claim.Value);

            return req;
        }

        private Request RemoveClaimRequest(TUser user, Claim claim)
        {
            const string q = @"
            query Q($user: string, $claimType: string, $claimValue) {
                u as var(func: uid($user))
                c as var(func: eq(claim.value, $claimValue)) @filter(eq(claim.type, $claimType))
            }";

            var mu = new Mutation
            {
                DelNquads = ByteString.CopyFromUtf8("uid(u) <claims> uid(c) ."),
                CommitNow = true,
                Cond = "@if(eq(len(u), 1) AND eq(len(c), 1))"
            };

            var req = new Request { Query = q, CommitNow = true };
            req.Mutations.Add(mu);
            req.Vars.Add("$user", user.Id);
            req.Vars.Add("$claimType", claim.Type);
            req.Vars.Add("$claimValue", claim.Value);

            return req;
        }

        /// <summary>
        /// Adds the <paramref name="claims"/> given to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the claim to.</param>
        /// <param name="claim">The claim to add to the user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public Task AddClaimAsync(TUser user, Claim claim, CancellationToken cancellationToken) =>
            AddClaimsAsync(user, new [] { claim }, cancellationToken);

        /// <summary>
        /// Adds the <paramref name="claims"/> given to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the claim to.</param>
        /// <param name="claims">The claim to add to the user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override async Task AddClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);
            claims ??= Array.Empty<Claim>();

            await using var txn = GetTransaction(cancellationToken);

            await txn.Do(claims.Select(claim => CreateClaimRequest(user, claim)));
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
            CheckUser(user, cancellationToken);
            CheckNull(claim, nameof(claim));
            CheckNull(newClaim, nameof(newClaim));

            await Task.WhenAll(RemoveClaimsAsync(user, new [] { claim }, cancellationToken), AddClaimAsync(user, newClaim, cancellationToken));
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
            CheckUser(user, cancellationToken);
            CheckNull(claims, nameof(claims));

            await using var txn = GetTransaction(cancellationToken);

            await txn.Do(claims.Select(claim => RemoveClaimRequest(user, claim)));
        }

        /// <summary>
        /// Adds the <paramref name="login"/> given to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the login to.</param>
        /// <param name="login">The login to add to the user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override async Task AddLoginAsync(TUser user, UserLoginInfo login, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);
            CheckNull(user, nameof(login));

            var userLogin = CreateUserLogin(user, login);

            var tUser = new TUser {
                Id = user.Id,
                Logins = new [] { userLogin }
            };

            const string q = @"
            query Q($user: string, $providerKey: string, $providerName: string, $loginProvider) {
                u as var(func: uid($user))
                p as var(func: eq(provider.key, $providerKey)) @filter(eq(provider.name, $providerName))
            }";

            var mu1 = new Mutation
            {
                SetNquads = ByteString.CopyFromUtf8(@"
                    uid(p) <provider.name> $providerName .
                    uid(p) <provider.key> $providerKey .
                    uid(p) <dgraph.type> Claim .
                    uid(p) <dgraph.type> Claim .
                "),
                CommitNow = true,
                Cond = "@if(eq(len(u), 1) AND eq(len(p), 0))"
            };

            var mu2 = new Mutation
            {
                SetNquads = ByteString.CopyFromUtf8("uid(u) <claims> uid(c) ."),
                CommitNow = false,
                Cond = "@if(eq(len(u), 1))"
            };

            var req = new Request { Query = q, CommitNow = true };
            req.Mutations.AddRange(new[] { mu1, mu2 });
            req.Vars.Add("$user", user.Id);
            req.Vars.Add("$claimType", claim.Type);
            req.Vars.Add("$claimValue", claim.Value);

            await using var txn = GetTransaction(cancellationToken);
            await txn.Mutate(new Mutation { CommitNow = true, SetJson = ByteString.FromBase64(JsonConvert.SerializeObject(tUser)) });
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
