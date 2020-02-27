using System;
using System.Collections.Generic;
using System.Linq;
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
    public class RoleStore : RoleStore<DRole>
    {
        private readonly ILogger<RoleStore> _logger;

        public RoleStore(ILogger<RoleStore> logger, DGraph context, IdentityErrorDescriber describer) : base(context, describer)
        {
            _logger = logger;
        }

        private IdentityResult CreateError(Exception exception)
        {
            var e = new IdentityError { Code = exception.Source, Description = exception.Message };
            _logger.LogError(exception, e.Description);

            return IdentityResult.Failed(e);
        }

        public override async Task<IdentityResult> CreateAsync(DRole role, CancellationToken cancellationToken = default)
        {
            CheckRole(role, cancellationToken);

            var q = $@"
            query Q($roleName: string) {{
                u as var(func: eq(dgraph.type, ""{IdentityTypeNameOptions.RoleTypeName}"")) @filter(eq(normalized_rolename, $roleName))
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
                var response = await txn.Do(req);

                return response.Uids.Count == 0 ? IdentityResult.Failed(ErrorDescriber.DuplicateUserName(role.NormalizedName)) : IdentityResult.Success;
            }
            catch (Exception ex)
            {
                var e = ErrorDescriber.DefaultError();
                _logger.LogError(ex, e.Description);

                return IdentityResult.Failed(e);
            }
        }

        public override async Task<IdentityResult> UpdateAsync(DRole role, CancellationToken cancellationToken = default)
        {
            CheckRole(role, cancellationToken);

            role.ConcurrencyStamp = Guid.NewGuid().ToString();

            var req = new Request { CommitNow = true, Query = $@"query Q($id: string) {{ u as var(func: uid($id))  @filter(eq(dgraph.type, ""{IdentityTypeNameOptions.RoleTypeName}"")) }}" };
            var mu = new Mutation { CommitNow = true, Cond = "@if(eq(len(u, 1)))", SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(role)) };
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

        public override async Task<IdentityResult> DeleteAsync(DRole role, CancellationToken cancellationToken = default)
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

        public override async Task<DRole> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed(cancellationToken);
            CheckNull(id, nameof(id));

            Uid roleId = id;

            var userResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars($@"query Q($roleId string) {{
                    role(func: uid($roleId)) @filter(eq(dgraph.type, ""{IdentityTypeNameOptions.RoleTypeName}"")) {{
                        uid
                        expand(_all_)
                    }}
                }}", new Dictionary<string, string> { { "$roleId", roleId } });

            return JsonConvert.DeserializeObject<Dictionary<string, List<DRole>>>(userResp.Json.ToStringUtf8())
                .First(x => x.Key == "role").Value?.FirstOrDefault();
        }

        public override async Task<DRole> FindByNameAsync(string normalizedName,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed(cancellationToken);
            CheckNull(normalizedName, nameof(normalizedName));

            var roleResp = await Context.NewTransaction(true, true, cancellationToken)
                .QueryWithVars($@"query Q($roleName string) {{
                    role(func: eq(normalized_rolename, $roleName)) @filter(eq(dgraph.type, ""{IdentityTypeNameOptions.RoleTypeName}"")) {{
                        uid
                        expand(_all_)
                        claims {{
                            uid
                            expand(_all_)
                        }}
                    }}
                }}", new Dictionary<string, string> { { "$roleName", normalizedName } });

            return JsonConvert.DeserializeObject<Dictionary<string, List<DRole>>>(roleResp.Json.ToStringUtf8())
                .First(x => x.Key == "role").Value?.FirstOrDefault();
        }

        public override async Task<IList<Claim>> GetClaimsAsync(DRole role, CancellationToken cancellationToken = default)
        {
            CheckRole(role, cancellationToken);

            if (!(role.Claims is null))
                return role.Claims.Select(c => c.ToClaim()).ToList();

            var rl = await FindByIdAsync(role.Id, cancellationToken);
            role.Populate(rl);

            return role.Claims.Select(c => c.ToClaim()).ToList();
        }

        private static Request CreateClaimRequest(DRole role, Claim claim)
        {
            var q = $@"
            query Q($roleId: string, $claimType: string) {{
                c as var(func: eq(claim_type, $claimType)) @filter(eq(dgraph.type, ""{IdentityTypeNameOptions.RoleClaimTypeName}"") AND eq(role_id, uid($roleId))))
            }}";

            var rc = DRoleClaim.InitializeFrom<DRoleClaim>(role, claim);

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

        private static Request RemoveClaimRequest(IEntity role, Claim claim)
        {
            var q = $@"
            query Q($roleId: string, $claimType: string) {{
                c as var(func: eq(claim_type, $claimType)) @filter(eq(dgraph.type, ""{IdentityTypeNameOptions.RoleClaimTypeName}"") AND eq(role_id, uid($roleId)))
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
        public override Task AddClaimAsync(DRole role, Claim claim, CancellationToken cancellationToken = default) =>
            AddClaimsAsync(role, new[] { claim }, cancellationToken);

        /// <summary>
        /// Adds the <paramref name="claims"/> given to the specified <paramref name="role"/>.
        /// </summary>
        /// <param name="role">The role to add the claim to.</param>
        /// <param name="claims">The claim to add to the role.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual async Task AddClaimsAsync(DRole role, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
        {
            CheckRole(role, cancellationToken);
            claims ??= Array.Empty<Claim>();

            await using var txn = GetTransaction(cancellationToken);

            try
            {
                await txn.Do(claims.Select(claim => CreateClaimRequest(role, claim)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                throw;
            }
        }

        public override Task RemoveClaimAsync(DRole role, Claim claim, CancellationToken cancellationToken = default)
            => RemoveClaimsAsync(role, new[] { claim }, cancellationToken);

        /// <summary>
        /// Removes the <paramref name="claims"/> given from the specified <paramref name="role"/>.
        /// </summary>
        /// <param name="role">The role to remove the claims from.</param>
        /// <param name="claims">The claim to remove.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual async Task RemoveClaimsAsync(DRole role, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
        {
            CheckRole(role, cancellationToken);
            CheckNull(claims, nameof(claims));

            await using var txn = GetTransaction(cancellationToken);

            try
            {
                await txn.Do(claims.Select(claim => RemoveClaimRequest(role, claim)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                throw;
            }
        }
    }

    /// <summary>
    /// Creates a new instance of a persistence store for roles.
    /// </summary>
    /// <typeparam name="TRole">The type of the class representing a role.</typeparam>
    public abstract class RoleStore<TRole> :
        IRoleClaimStore<TRole>,
        IAsyncDisposable
        where TRole : DRole, new()
    {
        /// <summary>
        /// Constructs a new instance of <see cref="RoleStoreBase{TRole, TKey, TUserRole, TRoleClaim}"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/>.</param>
        protected RoleStore(DGraph context, IdentityErrorDescriber describer)
        {
            Context = context;
            ErrorDescriber = describer ?? throw new ArgumentNullException(nameof(describer));
        }

        private bool _disposed;

        internal static IdentityTypeNameOptions<DUser, DRole, DUserToken, DUserClaim, DUserLogin, DRoleClaim>
            IdentityTypeNameOptions => new IdentityTypeNameOptions<DUser, DRole, DUserToken, DUserClaim, DUserLogin, DRoleClaim>();

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

        public DGraph Context { get; }

        /// <summary>
        /// Gets or sets the <see cref="IdentityErrorDescriber"/> for any error that occurred with the current operation.
        /// </summary>
        public IdentityErrorDescriber ErrorDescriber { get; set; }

        /// <summary>
        /// Creates a new role in a store as an asynchronous operation.
        /// </summary>
        /// <param name="role">The role to create in the store.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that represents the <see cref="IdentityResult"/> of the asynchronous query.</returns>
        public abstract Task<IdentityResult> CreateAsync(TRole role, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a role in a store as an asynchronous operation.
        /// </summary>
        /// <param name="role">The role to update in the store.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that represents the <see cref="IdentityResult"/> of the asynchronous query.</returns>
        public abstract Task<IdentityResult> UpdateAsync(TRole role, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a role from the store as an asynchronous operation.
        /// </summary>
        /// <param name="role">The role to delete from the store.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that represents the <see cref="IdentityResult"/> of the asynchronous query.</returns>
        public abstract Task<IdentityResult> DeleteAsync(TRole role, CancellationToken cancellationToken = default);

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
        /// Finds the role who has the specified ID as an asynchronous operation.
        /// </summary>
        /// <param name="id">The role ID to look for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that result of the look up.</returns>
        public abstract Task<TRole> FindByIdAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds the role who has the specified normalized name as an asynchronous operation.
        /// </summary>
        /// <param name="normalizedName">The normalized role name to look for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that result of the look up.</returns>
        public abstract Task<TRole> FindByNameAsync(string normalizedName,
            CancellationToken cancellationToken = default);

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

        /// <summary>
        /// Get the claims associated with the specified <paramref name="role"/> as an asynchronous operation.
        /// </summary>
        /// <param name="role">The role whose claims should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that contains the claims granted to a role.</returns>
        public abstract Task<IList<Claim>> GetClaimsAsync(TRole role, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds the <paramref name="claim"/> given to the specified <paramref name="role"/>.
        /// </summary>
        /// <param name="role">The role to add the claim to.</param>
        /// <param name="claim">The claim to add to the role.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public abstract Task AddClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the <paramref name="claim"/> given from the specified <paramref name="role"/>.
        /// </summary>
        /// <param name="role">The role to remove the claim from.</param>
        /// <param name="claim">The claim to remove from the role.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public abstract Task RemoveClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken = default);
    }
}
