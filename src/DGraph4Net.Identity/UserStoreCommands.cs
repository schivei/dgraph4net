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

            var req = new Request { CommitNow = true, Query = $@"query Q($id: string) {{ u as var(func: uid($id))  @filter(eq(dgraph.type, ""{IdentityTypeNameOptions.UserTypeName}"")) }}" };
            var mu = new Mutation { CommitNow = true, Cond = "@if(eq(len(u, 1)))", SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(user)) };
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

            var q = $@"
            query Q($userName: string, $email: string) {{
                u as var(func: eq(dgraph.type, ""{IdentityTypeNameOptions.UserTypeName}"")) @filter(eq(normalized_username, $userName) OR eq(normalized_email, $email))
            }}";

            var req = new Request
            {
                CommitNow = true,
                Query = q
            };

            req.Vars.Add("$userName", user.NormalizedUserName);
            req.Vars.Add("$email", user.NormalizedEmail);

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
        public override async Task AddToRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
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
        public override async Task RemoveFromRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
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

                if(ur != null)
                    user.Roles?.Remove(ur);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                throw;
            }
        }

        private Request CreateClaimRequest(TUser user, Claim claim)
        {
            var q = $@"
            query Q($userId: string, $claimType: string) {{
                c as var(func: eq(claim_type, $claimType)) @filter(eq(dgraph.type, ""{IdentityTypeNameOptions.UserClaimTypeName}"") AND eq(user_id, uid($userId))))
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

        private Request RemoveClaimRequest(TUser user, Claim claim)
        {
            var q = $@"
            query Q($userId: string, $claimType: string) {{
                c as var(func: eq(claim_type, $claimType)) @filter(eq(dgraph.type, ""{IdentityTypeNameOptions.UserClaimTypeName}"") AND eq(user_id, uid($userId))))
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
        /// Adds the <paramref name="claims"/> given to the specified <paramref name="user"/>.
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
        public override async Task AddClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
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
        public async override Task ReplaceClaimAsync(TUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken = default)
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
        public async override Task RemoveClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
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
        /// Adds the <paramref name="login"/> given to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the login to.</param>
        /// <param name="login">The login to add to the user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override async Task AddLoginAsync(TUser user, UserLoginInfo login, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);
            CheckNull(login, nameof(login));

            var userLogin = CreateUserLogin(user, login);

            var q = $@"
            query Q($userId: string, $providerKey: string, $providerName: string) {{
                p as var(func: eq(provider_key, $providerKey)) @filter(eq(login_provider, $providerName) AND eq(dgraph.type, ""{IdentityTypeNameOptions.UserLoginTypeName}"") AND eq(user_id, uid($userId)))
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
                FindUserLoginAsync(user.Id, userLogin.LoginProvider, userLogin.ProviderKey, cancellationToken);
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
        public override async Task RemoveLoginAsync(TUser user, string loginProvider, string providerKey, CancellationToken cancellationToken = default)
        {
            CheckUser(user, cancellationToken);
            CheckNull(loginProvider, nameof(loginProvider));
            CheckNull(providerKey, nameof(providerKey));

            var q = $@"
            query Q($userId: string, $providerKey: string, $providerName: string) {{
                p as var(func: eq(provider_key, $providerKey)) @filter(eq(login_provider, $providerName) AND eq(dgraph.type, ""{IdentityTypeNameOptions.UserLoginTypeName}"") AND eq(user_id, uid($userId)))
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
            await txn.Do(req);
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
