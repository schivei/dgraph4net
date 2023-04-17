using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using Dgraph4Net.OpenIddict.Models;

using OpenIddict.Abstractions;

using static OpenIddict.Abstractions.OpenIddictConstants;
using SR = OpenIddict.Abstractions.OpenIddictResources;
using Newtonsoft.Json;
using Api;
using Google.Protobuf;

namespace Dgraph4Net.OpenIddict.Stores
{
    /// <summary>
    /// Provides methods allowing to manage the authorizations stored in a database.
    /// </summary>
    /// <typeparam name="TAuthorization">The type of the Authorization entity.</typeparam>
    public class OpenIddictDgraphAuthorizationStore<TAuthorization> : IOpenIddictAuthorizationStore<TAuthorization>
        where TAuthorization : OpenIddictDgraphAuthorization, IEntity, new()
    {
        public OpenIddictDgraphAuthorizationStore(
            IOpenIddictDgraphContext context,
            IOptionsMonitor<OpenIddictDgraphOptions> options)
        {
            Context = context;
            Options = options;
        }

        /// <summary>
        /// Gets the database context associated with the current store.
        /// </summary>
        protected IOpenIddictDgraphContext Context { get; }

        /// <summary>
        /// Gets the options associated with the current store.
        /// </summary>
        protected IOptionsMonitor<OpenIddictDgraphOptions> Options { get; }

        /// <inheritdoc/>
        public virtual async ValueTask<long> CountAsync(CancellationToken cancellationToken)
        {

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var counts = await txn.Query<CountResult>("oidc_authorization", $@"{{
                oidc_authorization(func: type({Options.CurrentValue.AuthorizationTypeName})) {{
                    count(uid)
                }}
            }}");

            return counts.FirstOrDefault()?.Count ?? 0;
        }

        /// <inheritdoc/>
        public virtual async ValueTask<long> CountAsync<TResult>(
            Func<IQueryable<TAuthorization>, IQueryable<TResult>> query, CancellationToken cancellationToken)
        {
            if (query is null)
            {
                return await CountAsync(cancellationToken);
            }

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var authorizations = await txn.Query<TAuthorization>("oidc_authorization", Options.CurrentValue.AuthorizationFullQuery);

            return query(authorizations.AsQueryable()).LongCount();
        }

        /// <inheritdoc/>
        public virtual async ValueTask CreateAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(cancellationToken: cancellationToken);

            var mu = new Mutation
            {
                CommitNow = true,
                SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(authorization))
            };

            var response = await txn.Mutate(mu);

            if (response.Uids.Count == 0)
                throw new OpenIddictExceptions.ValidationException("One or more errors ocurred when try to create authorization.");
        }

        /// <inheritdoc/>
        public virtual async ValueTask DeleteAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            var auth = await FindByIdAsync(authorization.Id, cancellationToken) ??
                throw new InvalidDataException($"The authorization id '{authorization.Id}' is not valid.");

            authorization = auth!;

            var appId = authorization.ApplicationId;
            authorization.ApplicationId = default!;

            var database = await Context.GetDatabaseAsync(cancellationToken);

            var mu = new Mutation
            {
                CommitNow = true,
                DeleteJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(authorization))
            };

            using var txn = (Txn)database.NewTransaction(cancellationToken: cancellationToken);

            try
            {
                var response = await txn.Mutate(mu);

                var resp = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Json.ToStringUtf8());

                if (!resp.TryGetValue("code", out var s) || s?.ToString() != "Success")
                {
                    throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0241));
                }
            }
            finally
            {
                authorization.ApplicationId = appId;
            }
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TAuthorization> FindAsync(
            string subject, string client, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0198), nameof(subject));
            }

            if (string.IsNullOrEmpty(client))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0124), nameof(client));
            }

            var part = $"type({Options.CurrentValue.AuthorizationTypeName}))";

            var query = "query Q($client: string, $subject: string)" + Options.CurrentValue.AuthorizationFullQuery
                .Replace(part, $"eq(oidc_subject, $subject)) @cascade @filter({part}")
                .Replace("oidc_app_auth", "oidc_app_auth @filter(eq(oidc_client_id, $client))");

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var result = await txn.QueryWithVars<TAuthorization>("oidc_authorization", query, new()
            {
                ["client"] = client,
                ["subject"] = subject
            });

            foreach (var auth in result)
            {
                yield return auth;
            }
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TAuthorization> FindAsync(
            string subject, string client,
            string status, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0198), nameof(subject));
            }

            if (string.IsNullOrEmpty(client))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0124), nameof(client));
            }

            if (string.IsNullOrEmpty(status))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0199), nameof(status));
            }

            var part = $"type({Options.CurrentValue.AuthorizationTypeName}))";

            var query = "query Q($client: string, $subject: string, $status: string)" + Options.CurrentValue.AuthorizationFullQuery
                .Replace(part, $"eq(oidc_subject, $subject) AND eq(oidc_status, $status)) @cascade @filter({part}")
                .Replace("oidc_app_auth", "oidc_app_auth @filter(eq(oidc_client_id, $client))");

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var result = await txn.QueryWithVars<TAuthorization>("oidc_authorization", query, new()
            {
                ["client"] = client,
                ["subject"] = subject,
                ["status"] = status
            });

            foreach (var auth in result)
            {
                yield return auth;
            }
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TAuthorization> FindAsync(
            string subject, string client,
            string status, string type, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0198), nameof(subject));
            }

            if (string.IsNullOrEmpty(client))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0124), nameof(client));
            }

            if (string.IsNullOrEmpty(status))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0199), nameof(status));
            }

            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0200), nameof(type));
            }

            var part = $"type({Options.CurrentValue.AuthorizationTypeName}))";

            var query = "query Q($client: string, $subject: string, $status: string, $type: string)" + Options.CurrentValue.AuthorizationFullQuery
                .Replace(part, $"eq(oidc_subject, $subject) AND eq(oidc_status, $status) AND eq(oidc_type, $type)) @cascade @filter({part}")
                .Replace("oidc_app_auth", "oidc_app_auth @filter(eq(oidc_client_id, $client))");

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var result = await txn.QueryWithVars<TAuthorization>("oidc_authorization", query, new()
            {
                ["client"] = client,
                ["subject"] = subject,
                ["status"] = status,
                ["type"] = type
            });

            foreach (var auth in result)
            {
                yield return auth;
            }
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TAuthorization> FindAsync(
            string subject, string client,
            string status, string type,
            ImmutableArray<string> scopes, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0198), nameof(subject));
            }

            if (string.IsNullOrEmpty(client))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0124), nameof(client));
            }

            if (string.IsNullOrEmpty(status))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0199), nameof(status));
            }

            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0200), nameof(type));
            }

            var part = $"type({Options.CurrentValue.AuthorizationTypeName}))";

            var query = "query Q($client: string, $subject: string, $status: string, $type: string)" + Options.CurrentValue.AuthorizationFullQuery
                .Replace(part, $"eq(oidc_subject, $subject) AND eq(oidc_status, $status) AND eq(oidc_type, $type) AND eq(oidc_scopes, {JsonConvert.SerializeObject(scopes.ToArray())})) @cascade @filter({part}")
                .Replace("oidc_app_auth", "oidc_app_auth @filter(eq(oidc_client_id, $client))");

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var result = await txn.QueryWithVars<TAuthorization>("oidc_authorization", query, new()
            {
                ["client"] = client,
                ["subject"] = subject,
                ["status"] = status,
                ["type"] = type
            });

            foreach (var auth in result)
            {
                yield return auth;
            }
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TAuthorization> FindByApplicationIdAsync(
            string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
            }

            var part = $"type({Options.CurrentValue.AuthorizationTypeName}))";

            var query = "query Q($client: string)" + Options.CurrentValue.AuthorizationFullQuery
                .Replace(part, $"{part} @cascade")
                .Replace("oidc_app_auth", "oidc_app_auth @filter(uid($client))");

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var result = await txn.QueryWithVars<TAuthorization>("oidc_authorization", query, new()
            {
                ["client"] = identifier
            });

            foreach (var auth in result)
            {
                yield return auth;
            }
        }

        /// <inheritdoc/>
        public virtual async ValueTask<TAuthorization?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
            }

            var part = $"type({Options.CurrentValue.AuthorizationTypeName}))";

            var query = "query Q($id: string)" + Options.CurrentValue.AuthorizationFullQuery
                .Replace(part, $"uid($id)) @filter({part}");

            var database = await Context.GetDatabaseAsync(cancellationToken);
            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var result = await txn.QueryWithVars<TAuthorization>("oidc_authorization", query, new()
            {
                ["id"] = identifier
            });

            return result.Cast<TAuthorization?>().FirstOrDefault();
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TAuthorization> FindBySubjectAsync(
            string subject, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0198), nameof(subject));
            }

            var part = $"type({Options.CurrentValue.AuthorizationTypeName}))";

            var query = "query Q($subject: string)" + Options.CurrentValue.AuthorizationFullQuery
                .Replace(part, $"eq(oidc_subject, $subject)) @filter({part}");

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var result = await txn.QueryWithVars<TAuthorization>("oidc_authorization", query, new()
            {
                ["subject"] = subject
            });

            foreach (var auth in result)
            {
                yield return auth;
            }
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetApplicationIdAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            return new ValueTask<string?>(authorization.ApplicationId);
        }

        /// <inheritdoc/>
        public virtual async ValueTask<TResult> GetAsync<TState, TResult>(
            Func<IQueryable<TAuthorization>, TState, IQueryable<TResult>> query,
            TState state, CancellationToken cancellationToken)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var authorizations = await txn.Query<TAuthorization>("oidc_authorization", Options.CurrentValue.AuthorizationFullQuery);

            return query(authorizations.AsQueryable(), state).FirstOrDefault() ?? default!;
        }

        /// <inheritdoc/>
        public virtual ValueTask<DateTimeOffset?> GetCreationDateAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            if (authorization.CreationDate is null)
            {
                return new ValueTask<DateTimeOffset?>(result: null);
            }

            return new ValueTask<DateTimeOffset?>(DateTime.SpecifyKind(authorization.CreationDate.Value, DateTimeKind.Utc));
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetIdAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            return new ValueTask<string?>(authorization.Id);
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            if (authorization.Properties is null)
            {
                return new ValueTask<ImmutableDictionary<string, JsonElement>>(ImmutableDictionary.Create<string, JsonElement>());
            }

            var document = JsonConvert.DeserializeObject<Dictionary<string, JsonElement>>(ByteString.CopyFrom(authorization.Properties).ToStringUtf8());

            return new ValueTask<ImmutableDictionary<string, JsonElement>>(document.ToImmutableDictionary());
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableArray<string>> GetScopesAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            if (authorization.Scopes is null || authorization.Scopes.Count == 0)
            {
                return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
            }

            return new ValueTask<ImmutableArray<string>>(authorization.Scopes.ToImmutableArray());
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetStatusAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            return new ValueTask<string?>(authorization.Status);
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetSubjectAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            return new ValueTask<string?>(authorization.Subject);
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetTypeAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            return new ValueTask<string?>(authorization.Type);
        }

        /// <inheritdoc/>
        public virtual ValueTask<TAuthorization> InstantiateAsync(CancellationToken cancellationToken)
        {
            try
            {
                return new ValueTask<TAuthorization>(Activator.CreateInstance<TAuthorization>());
            }

            catch (MemberAccessException exception)
            {
                return new ValueTask<TAuthorization>(Task.FromException<TAuthorization>(
                    new InvalidOperationException(SR.GetResourceString(SR.ID0242), exception)));
            }
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TAuthorization> ListAsync(
            int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            count ??= 10;
            offset ??= 0;

            var partN = "oidc_authorization";
            var partF = $"(func: type({Options.CurrentValue.AuthorizationTypeName}))";

            var take = $", first: {count}";
            var skip = offset! > 0 ? $", offset: {offset}" : "";

            var query = Options.CurrentValue.AuthorizationFullQuery.Replace(partF, $"(func: uid(id){take}{skip})")
                .Replace(partN, @$"
                id as var{partF}
                {partN}");

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var applications = await txn.Query<TAuthorization>(partN, query);

            foreach (var application in applications)
            {
                yield return application;
            }
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
            Func<IQueryable<TAuthorization>, TState, IQueryable<TResult>> query,
            TState state, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var applications = await txn.Query<TAuthorization>("oidc_authorization", Options.CurrentValue.AuthorizationFullQuery);

            await foreach (var element in query(applications.AsQueryable(), state).ToAsyncEnumerable())
            {
                yield return element;
            }
        }

        /// <inheritdoc/>
        public virtual async ValueTask PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
        {
            var query = $@"query Q($time: string, $status: string, $type: string) {{
              auth as var(func: lt(oidc_creation_date, $time)) @filter(type({Options.CurrentValue.AuthorizationTypeName}) AND (NOT eq(oidc_status, $status) OR (eq(count(~oidc_auth_token), 0) AND eq(oid_type, $type))))
              token as var(func: type({Options.CurrentValue.TokenTypeName})) @cascade {{
                oidc_auth_token @filter(uid(auth))
              }}
            }}";

            var m1 = new Mutation
            {
                CommitNow = true,
                Cond = "@if(gt(len(token), 0))",
                DelNquads = ByteString.CopyFromUtf8("uid(token) * * .")
            };

            var m2 = new Mutation {
                CommitNow = true,
                Cond = "@if(gt(len(auth), 0))",
                DelNquads = ByteString.CopyFromUtf8("uid(auth) * * .")
            };

            var req = new Request
            {
                CommitNow = true,
                Query = query
            };

            req.Mutations.AddRange(new[] { m1, m2 });

            req.Vars.Add(new Dictionary<string, string> {
                ["time"] = threshold.UtcDateTime.ToString("O"),
                ["status"] = Statuses.Valid,
                ["type"] = AuthorizationTypes.AdHoc
            });

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var response = await txn.Do(req);

            var resp = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Json.ToStringUtf8());

            if (!resp.TryGetValue("code", out var s) || s?.ToString() != "Success")
            {
                throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0241));
            }
        }

        /// <inheritdoc/>
        public virtual ValueTask SetApplicationIdAsync(TAuthorization authorization,
            string? identifier, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            if (!string.IsNullOrEmpty(identifier))
            {
                authorization.ApplicationId = identifier;
            }
            else
            {
                authorization.ApplicationId = Uid.NewUid();
            }

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetCreationDateAsync(TAuthorization authorization,
            DateTimeOffset? date, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            authorization.CreationDate = date?.UtcDateTime;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetPropertiesAsync(TAuthorization authorization,
            ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            if (properties is null || properties.IsEmpty)
            {
                authorization.Properties = Array.Empty<byte>();

                return default;
            }

            authorization.Properties = ByteString.CopyFromUtf8(JsonConvert
                .SerializeObject(properties.ToDictionary(k => k.Key, v => v.Value))).ToByteArray();

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetScopesAsync(TAuthorization authorization,
            ImmutableArray<string> scopes, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            if (scopes.IsDefaultOrEmpty)
            {
                authorization.Scopes = new();

                return default;
            }

            authorization.Scopes = scopes.ToList();

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetStatusAsync(TAuthorization authorization, string? status, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            authorization.Status = status;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetSubjectAsync(TAuthorization authorization, string? subject, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            authorization.Subject = subject;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetTypeAsync(TAuthorization authorization, string? type, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            authorization.Type = type;

            return default;
        }

        /// <inheritdoc/>
        public virtual async ValueTask UpdateAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            // Generate a new concurrency token and attach it
            // to the authorization before persisting the changes.
            var timestamp = authorization.ConcurrencyToken;
            authorization.ConcurrencyToken = Guid.NewGuid();

            var database = await Context.GetDatabaseAsync(cancellationToken);

            if (authorization.Id.IsEmpty || authorization.Id.IsReferenceOnly)
            {
                await CreateAsync(authorization, cancellationToken);
                return;
            }

            var type = Options.CurrentValue.AuthorizationTypeName;

            var req = new Request { CommitNow = true, Query = $@"query Q($id: string, $ct: string) {{ u as var(func: uid($id) AND eq(oidc_concurrency_token, $ct))  @filter(type({type})) }}" };

            req.Vars.Add(new Dictionary<string, string>
            {
                ["id"] = authorization.Id,
                ["ct"] = timestamp.ToString()
            });

            var mu = new Mutation
            {
                CommitNow = true,
                Cond = "@if(eq(len(u, 1)))",
                SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(authorization, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Include,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                }))
            };

            req.Mutations.Add(mu);

            using var txn = (Txn)database.NewTransaction(cancellationToken: cancellationToken);

            try
            {
                var response = await txn.Do(req);

                var resp = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Json.ToStringUtf8());

                if (!resp.TryGetValue("code", out var s) || s?.ToString() != "Success")
                {
                    throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0241));
                }
            }
            catch (Exception e)
            {
                if (e is OpenIddictExceptions.ConcurrencyException)
                    throw;

                throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0241), e);
            }
        }
    }
}
