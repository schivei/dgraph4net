using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Dgraph4Net.Services;
using Google.Protobuf;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Dgraph4Net.Identity
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "<Pending>")]
    public class RoleStore : RoleStore<DRole, DRoleClaim>
    {
        public RoleStore(ILogger<RoleStore> logger, Dgraph4NetClient context,
            IdentityErrorDescriber describer) :
            base(logger, context, describer)
        {
        }
    }

    /// <summary>
    /// Creates a new instance of a persistence store for roles.
    /// </summary>
    /// <typeparam name="TRole">The type of the class representing a role.</typeparam>
    /// <typeparam name="TRoleClaim"></typeparam>
    public class RoleStore<TRole, TRoleClaim> :
        IRoleClaimStore<TRole>,
        IAsyncDisposable
        where TRole : DRole<TRole, TRoleClaim>, new()
        where TRoleClaim : DRoleClaim<TRoleClaim, TRole>, new()
    {
        /// <summary>
        /// Constructs a new instance of <see cref="RoleStoreBase{TRole, TKey, TUserRole, TRoleClaim}"/>.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="context"></param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/>.</param>
        protected RoleStore(ILogger<RoleStore<TRole, TRoleClaim>> logger,
            Dgraph4NetClient context, IdentityErrorDescriber describer)
        {
            _logger = logger;
            Context = context;
            ErrorDescriber = describer ?? throw new ArgumentNullException(nameof(describer));
        }

        private readonly ILogger<RoleStore<TRole, TRoleClaim>> _logger;

        private IdentityResult CreateError(Exception exception)
        {
            var e = new IdentityError { Code = exception.Source, Description = exception.Message };
            _logger.LogError(exception, e.Description);

            return IdentityResult.Failed(e);
        }

        public virtual async Task<IdentityResult> CreateAsync(TRole role, CancellationToken cancellationToken = default)
        {
            CheckRole(role, cancellationToken);

            var rtn = new TRole().GetDType();

            var q = $@"
            query Q($roleName: string) {{
                u as var(func: type({rtn})) @filter(eq(normalized_rolename, $roleName))
            }}";

            var req = new Request
            {
                CommitNow = true,
                Query = q
            };

            req.Vars.Add("$roleName", role.NormalizedName);

            var mu = new Mutation
            {
                CommitNow = true,
                Cond = "@if(eq(len(u), 0))",
                SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(role))
            };

            req.Mutations.Add(mu);

            await using var txn = GetTransaction(cancellationToken);

            try
            {
                var response = await txn.Do(req).ConfigureAwait(false);

                return response.Uids.Count == 0 ? IdentityResult.Failed(ErrorDescriber.DuplicateUserName(role.NormalizedName)) : IdentityResult.Success;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                var e = ErrorDescriber.DefaultError();
                _logger.LogError(ex, e.Description);

                return IdentityResult.Failed(e);
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        public virtual async Task<IdentityResult> UpdateAsync(TRole role, CancellationToken cancellationToken = default)
        {
            CheckRole(role, cancellationToken);

            role.ConcurrencyStamp = Guid.NewGuid().ToString();

            var rtn = new TRole().GetDType();

            var req = new Request { CommitNow = true, Query = $@"query Q($id: string) {{ u as var(func: uid($id))  @filter(type({rtn})) }}" };
            var mu = new Mutation { CommitNow = true, Cond = "@if(eq(len(u, 1)))", SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(role)) };
            req.Mutations.Add(mu);
            await using var txn = GetTransaction(cancellationToken);
            try
            {
                await txn.Do(req).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                var e = ErrorDescriber.ConcurrencyFailure();
                _logger.LogError(ex, e.Description);

                return IdentityResult.Failed(e);
            }
#pragma warning restore CA1031 // Do not catch general exception types
            return IdentityResult.Success;
        }

        public virtual async Task<IdentityResult> DeleteAsync(TRole role, CancellationToken cancellationToken = default)
        {
            CheckRole(role, cancellationToken);

            var mu = new Mutation
            {
                CommitNow = true,
                DeleteJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(role))
            };

            await using var txn = GetTransaction(cancellationToken);

            try
            {
                var response = await txn.Mutate(mu).ConfigureAwait(false);
                var resp = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Json.ToStringUtf8());

                if (!resp.TryGetValue("code", out var s) || s?.ToString() != "Success")
                    return IdentityResult.Failed(ErrorDescriber.DefaultError());
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                return CreateError(ex);
            }
#pragma warning restore CA1031 // Do not catch general exception types

            return IdentityResult.Success;
        }

        /// <summary>
        /// Finds the role who has the specified ID as an asynchronous operation.
        /// </summary>
        /// <param name="id">The role ID to look for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that result of the look up.</returns>
        public virtual async Task<TRole> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed(cancellationToken);
            CheckNull(id, nameof(id));

            Uid roleId = id;

            var rtn = new TRole().GetDType();

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars($@"query Q($roleId: string) {{
                    role(func: uid($roleId)) @filter(type({rtn})) {{
                        uid
                        expand(_all_)
                    }}
                }}", new Dictionary<string, string> { { "$roleId", roleId } })
                .ConfigureAwait(false);

            return JsonConvert.DeserializeObject<Dictionary<string, List<TRole>>>(userResp.Json.ToStringUtf8())
                .First(x => x.Key == "role").Value?.FirstOrDefault();
        }

        public virtual async Task<TRole> FindByNameAsync(string normalizedName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed(cancellationToken);
            CheckNull(normalizedName, nameof(normalizedName));

            var rtn = new TRole().GetDType();

            var roleResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars($@"query Q($roleName: string) {{
                    role(func: eq(normalized_rolename, $roleName)) @filter(type({rtn})) {{
                        uid
                        expand(_all_)
                        claims {{
                            uid
                            expand(_all_)
                        }}
                    }}
                }}", new Dictionary<string, string> { { "$roleName", normalizedName } })
                .ConfigureAwait(false);

            return JsonConvert.DeserializeObject<Dictionary<string, List<TRole>>>(roleResp.Json.ToStringUtf8())
                .First(x => x.Key == "role").Value?.FirstOrDefault();
        }

        public virtual async Task<IList<Claim>> GetClaimsAsync(TRole role, CancellationToken cancellationToken = default)
        {
            CheckRole(role, cancellationToken);

            if (!(role.Claims is null))
                return role.Claims.Select(c => new Claim(c.ClaimType, c.ClaimValue)).ToList();

            var rl = await FindByIdAsync(role.Id, cancellationToken)
                .ConfigureAwait(false);

            role.Claims = rl.Claims;

            return role.Claims.Select(c => new Claim(c.ClaimType, c.ClaimValue)).ToList();
        }

        private static Request CreateClaimRequest(TRole role, Claim claim)
        {
            var rctn = new TRoleClaim().GetDType();

            var q = $@"
            query Q($roleId: string, $claimType: string) {{
                c as var(func: eq(claim_type, $claimType)) @filter(type({rctn}) AND eq(role_id, uid($roleId))))
            }}";

            var rc = DRoleClaim<TRoleClaim, TRole>.InitializeFrom(role, claim);

            var mu = new Mutation
            {
                SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(rc)),
                CommitNow = true,
                Cond = "@if(eq(len(c), 0))"
            };

            var req = new Request { Query = q, CommitNow = true };
            req.Mutations.Add(mu);
            req.Vars.Add("$roleId", role.Id);
            req.Vars.Add("$claimType", claim.Type);

            return req;
        }

        private static Request RemoveClaimRequest(TRole role, Claim claim)
        {
            var rctn = new TRoleClaim().GetDType();

            var q = $@"
            query Q($roleId: string, $claimType: string) {{
                c as var(func: eq(claim_type, $claimType)) @filter(type({rctn}) AND eq(role_id, uid($roleId)))
            }}";

            var mu = new Mutation
            {
                DelNquads = ByteString.CopyFromUtf8("uid(c) * * ."),
                CommitNow = true,
                Cond = "@if(eq(len(c), 1))"
            };

            var req = new Request { Query = q, CommitNow = true };
            req.Mutations.Add(mu);
            req.Vars.Add("$roleId", role.Id);
            req.Vars.Add("$claimType", claim.Type);

            return req;
        }

        /// <summary>
        /// Adds the <paramref name="claim"/> given to the specified <paramref name="role"/>.
        /// </summary>
        /// <param name="role">The role to add the claim to.</param>
        /// <param name="claim">The claim to add to the role.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task AddClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken = default) =>
            AddClaimsAsync(role, new[] { claim }, cancellationToken);

        /// <summary>
        /// Adds the <paramref name="claims"/> given to the specified <paramref name="role"/>.
        /// </summary>
        /// <param name="role">The role to add the claim to.</param>
        /// <param name="claims">The claim to add to the role.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual async Task AddClaimsAsync(TRole role, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
        {
            CheckRole(role, cancellationToken);
            claims ??= Array.Empty<Claim>();

            await using var txn = GetTransaction(cancellationToken);

            try
            {
                await txn.Do(claims.Select(claim => CreateClaimRequest(role, claim)))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                throw;
            }
        }

        public virtual Task RemoveClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken = default)
            => RemoveClaimsAsync(role, new[] { claim }, cancellationToken);

        /// <summary>
        /// Removes the <paramref name="claims"/> given from the specified <paramref name="role"/>.
        /// </summary>
        /// <param name="role">The role to remove the claims from.</param>
        /// <param name="claims">The claim to remove.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual async Task RemoveClaimsAsync(TRole role, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
        {
            CheckRole(role, cancellationToken);
            CheckNull(claims, nameof(claims));

            await using var txn = GetTransaction(cancellationToken);

            try
            {
                await txn.Do(claims.Select(claim => RemoveClaimRequest(role, claim)))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                throw;
            }
        }

        private bool _disposed;

        protected Txn GetTransaction(CancellationToken cancellationToken = default) =>
            Context.NewTransaction(cancellationToken: cancellationToken);

        /// <summary>
        /// Throws if this class has been disposed.
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
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

            // ReSharper disable once MethodSupportsCancellation
            ThrowIfDisposed();
        }

        protected static void CheckNull<T>(T obj, string paramName)
        {
            if (obj is null)
                throw new ArgumentException("{0} can not be null.", paramName);
        }

        protected void CheckRole(TRole role, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed(cancellationToken);
            CheckNull(role, nameof(role));
        }

        public virtual Dgraph4NetClient Context { get; }

        /// <summary>
        /// Gets or sets the <see cref="IdentityErrorDescriber"/> for any error that occurred with the current operation.
        /// </summary>
        public IdentityErrorDescriber ErrorDescriber { get; set; }

        /// <summary>
        /// Gets the ID for a role from the store as an asynchronous operation.
        /// </summary>
        /// <param name="role">The role whose ID should be returned.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that contains the ID of the role.</returns>
        public virtual Task<string> GetRoleIdAsync(TRole role, CancellationToken cancellationToken = default)
        {
            CheckRole(role, cancellationToken);

            return Task.FromResult(ConvertIdToString(role.Id));
        }

        /// <summary>
        /// Gets the name of a role from the store as an asynchronous operation.
        /// </summary>
        /// <param name="role">The role whose name should be returned.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that contains the name of the role.</returns>
        public virtual Task<string> GetRoleNameAsync(TRole role, CancellationToken cancellationToken = default)
        {
            CheckRole(role, cancellationToken);

            return Task.FromResult(role.Name);
        }

        /// <summary>
        /// Sets the name of a role in the store as an asynchronous operation.
        /// </summary>
        /// <param name="role">The role whose name should be set.</param>
        /// <param name="roleName">The name of the role.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task SetRoleNameAsync(TRole role, string roleName, CancellationToken cancellationToken = default)
        {
            CheckRole(role, cancellationToken);
            role.Name = roleName;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Converts the provided <paramref name="id"/> to its string representation.
        /// </summary>
        /// <param name="id">The id to convert.</param>
        /// <returns>An <see cref="string"/> representation of the provided <paramref name="id"/>.</returns>
        public virtual string ConvertIdToString(Uid id) =>
            id;

        /// <summary>
        /// Get a role's normalized name as an asynchronous operation.
        /// </summary>
        /// <param name="role">The role whose normalized name should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that contains the name of the role.</returns>
        public virtual Task<string> GetNormalizedRoleNameAsync(TRole role,
            CancellationToken cancellationToken = default)
        {
            CheckRole(role, cancellationToken);
            return Task.FromResult(role.NormalizedName);
        }

        /// <summary>
        /// Set a role's normalized name as an asynchronous operation.
        /// </summary>
        /// <param name="role">The role whose normalized name should be set.</param>
        /// <param name="normalizedName">The normalized name to set</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task SetNormalizedRoleNameAsync(TRole role, string normalizedName,
            CancellationToken cancellationToken = default)
        {
            CheckRole(role, cancellationToken);
            role.NormalizedName = normalizedName;
            return Task.CompletedTask;
        }

        ~RoleStore()
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
    }
}
