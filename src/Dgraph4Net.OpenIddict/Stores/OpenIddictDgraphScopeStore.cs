using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using Dgraph4Net.OpenIddict.Models;

using OpenIddict.Abstractions;

using SR = OpenIddict.Abstractions.OpenIddictResources;
using Newtonsoft.Json;
using Api;
using Google.Protobuf;

namespace Dgraph4Net.OpenIddict.Stores
{
    /// <summary>
    /// Provides methods allowing to manage the scopes stored in a database.
    /// </summary>
    /// <typeparam name="TScope">The type of the Scope entity.</typeparam>
    public class OpenIddictDgraphScopeStore<TScope> : IOpenIddictScopeStore<TScope>
        where TScope : OpenIddictDgraphScope, IEntity, new()
    {
        public OpenIddictDgraphScopeStore(
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

            var counts = await txn.Query<CountResult>("oidc_scope", $@"{{
                oidc_scope(func: type({Options.CurrentValue.ScopeTypeName})) {{
                    count(uid)
                }}
            }}");

            return counts.FirstOrDefault()?.Count ?? 0;
        }

        /// <inheritdoc/>
        public virtual async ValueTask<long> CountAsync<TResult>(
            Func<IQueryable<TScope>, IQueryable<TResult>> query, CancellationToken cancellationToken)
        {
            if (query is null)
            {
                return await CountAsync(cancellationToken);
            }

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var scopes = await txn.Query<TScope>("oidc_scope", Options.CurrentValue.ScopeFullQuery);

            return query(scopes.AsQueryable()).LongCount();
        }

        /// <inheritdoc/>
        public virtual async ValueTask CreateAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(cancellationToken: cancellationToken);

            var req = new Request { CommitNow = true };

            var type = Options.CurrentValue.ApplicationTypeName;

            var mu = new Mutation
            {
                CommitNow = true,
                SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(scope))
            };

            if (scope.Name is not null)
            {
                var query = $@"query Q($name: string) {{ u as var(func: eq({scope.GetColumnName("Name")}, $name))  @filter(type({type})) }}";

                req.Vars.Add(new Dictionary<string, string>
                {
                    ["name"] = scope.Name
                });

                mu.Cond = "@if(eq(len(u, 1)))";
            }

            req.Mutations.Add(mu);

            var response = await txn.Do(req);

            if (response.Uids.Count == 0)
            {
                var resp = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Json.ToStringUtf8());

                if (!resp.TryGetValue("code", out var s) || s?.ToString() != "Success")
                {
                    throw new OpenIddictExceptions.ConcurrencyException("Already exists an scope with the same 'Name'.");
                }

                throw new OpenIddictExceptions.GenericException("One or more errors ocurred when try to create scope.");
            }
        }

        /// <inheritdoc/>
        public virtual async ValueTask DeleteAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            var scp = await FindByIdAsync(scope.Id, cancellationToken) ??
                throw new InvalidDataException($"The scope id '{scope.Id}' is not valid.");

            scope = scp!;

            var database = await Context.GetDatabaseAsync(cancellationToken);

            var mu = new Mutation
            {
                CommitNow = true,
                DeleteJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(scope))
            };

            using var txn = (Txn)database.NewTransaction(cancellationToken: cancellationToken);

            var response = await txn.Mutate(mu);

            var resp = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Json.ToStringUtf8());

            if (!resp.TryGetValue("code", out var s) || s?.ToString() != "Success")
            {
                throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0241));
            }
        }

        /// <inheritdoc/>
        public virtual async ValueTask<TScope?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
            }

            var part = $"type({Options.CurrentValue.ScopeTypeName}))";

            var query = "query Q($id: string)" + Options.CurrentValue.ScopeFullQuery
                .Replace(part, $"uid($id)) @filter({part}");

            var database = await Context.GetDatabaseAsync(cancellationToken);
            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var result = await txn.QueryWithVars<TScope>("oidc_scope", query, new()
            {
                ["id"] = identifier
            });

            return result.Cast<TScope?>().FirstOrDefault();
        }

        /// <inheritdoc/>
        public virtual async ValueTask<TScope?> FindByNameAsync(string name, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0202), nameof(name));
            }

            var part = $"type({Options.CurrentValue.ScopeTypeName}))";

            var ts = typeof(TScope);

            var query = "query Q($name: string)" + Options.CurrentValue.ScopeFullQuery
                .Replace(part, $"eq({ts.As<TScope>().GetColumnName(c => c.Name)}, $name)) @filter({part}");

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var result = await txn.QueryWithVars<TScope>("oidc_scope", query, new()
            {
                ["name"] = name
            });

            return result.Cast<TScope?>().FirstOrDefault();
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TScope> FindByNamesAsync(ImmutableArray<string> names, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (names.Any(name => string.IsNullOrEmpty(name)))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0203), nameof(names));
            }

            var part = $"type({Options.CurrentValue.ScopeTypeName}))";

            var ts = typeof(TScope);

            var query = Options.CurrentValue.ScopeFullQuery
                .Replace(part, $"eq({ts.As<TScope>().GetColumnName(c => c.Name)}, {JsonConvert.SerializeObject(names.ToArray())})) @filter({part}");

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var result = await txn.Query<TScope>("oidc_scope", query);

            foreach (var item in result)
            {
                yield return item;
            }
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TScope> FindByResourceAsync(string resource, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(resource))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0062), nameof(resource));
            }

            var part = $"type({Options.CurrentValue.ScopeTypeName}))";

            var ts = typeof(TScope);

            var query = "query Q($resource: string)" + Options.CurrentValue.ScopeFullQuery
                .Replace(part, $"eq({ts.As<TScope>().GetColumnName(c => c.Resources)}, $resource)) @filter({part}");

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var result = await txn.QueryWithVars<TScope>("oidc_scope", query, new()
            {
                ["resource"] = resource
            });

            foreach (var item in result)
            {
                yield return item;
            }
        }

        /// <inheritdoc/>
        public virtual async ValueTask<TResult> GetAsync<TState, TResult>(
            Func<IQueryable<TScope>, TState, IQueryable<TResult>> query,
            TState state, CancellationToken cancellationToken)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var scopes = await txn.Query<TScope>("oidc_scope", Options.CurrentValue.ScopeFullQuery);

            return query(scopes.AsQueryable(), state).FirstOrDefault() ?? default!;
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetDescriptionAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            return new ValueTask<string?>(scope.Description);
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableDictionary<CultureInfo, string>> GetDescriptionsAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (scope.Descriptions is null || scope.Descriptions.Count == 0)
            {
                return new ValueTask<ImmutableDictionary<CultureInfo, string>>(ImmutableDictionary.Create<CultureInfo, string>());
            }

            return new ValueTask<ImmutableDictionary<CultureInfo, string>>(scope.Descriptions.ToDictionary().ToImmutableDictionary());
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetDisplayNameAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            return new ValueTask<string?>(scope.DisplayName);
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (scope.DisplayNames is null || scope.DisplayNames.Count == 0)
            {
                return new ValueTask<ImmutableDictionary<CultureInfo, string>>(ImmutableDictionary.Create<CultureInfo, string>());
            }

            return new ValueTask<ImmutableDictionary<CultureInfo, string>>(scope.DisplayNames.ToDictionary().ToImmutableDictionary());
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetIdAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            return new ValueTask<string?>(scope.Id.ToString());
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetNameAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            return new ValueTask<string?>(scope.Name);
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (scope.Properties is null)
            {
                return new ValueTask<ImmutableDictionary<string, JsonElement>>(ImmutableDictionary.Create<string, JsonElement>());
            }

            var document = JsonConvert.DeserializeObject<Dictionary<string, JsonElement>>(ByteString.CopyFrom(scope.Properties).ToStringUtf8());

            return new ValueTask<ImmutableDictionary<string, JsonElement>>(document.ToImmutableDictionary());
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableArray<string>> GetResourcesAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (scope.Resources is null || scope.Resources.Count == 0)
            {
                return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
            }

            return new ValueTask<ImmutableArray<string>>(scope.Resources.ToImmutableArray());
        }

        /// <inheritdoc/>
        public virtual ValueTask<TScope> InstantiateAsync(CancellationToken cancellationToken)
        {
            try
            {
                return new ValueTask<TScope>(Activator.CreateInstance<TScope>());
            }

            catch (MemberAccessException exception)
            {
                return new ValueTask<TScope>(Task.FromException<TScope>(
                    new InvalidOperationException(SR.GetResourceString(SR.ID0246), exception)));
            }
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TScope> ListAsync(
            int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            count ??= 10;
            offset ??= 0;

            var partN = "oidc_scope";
            var partF = $"(func: type({Options.CurrentValue.ScopeTypeName}))";

            var take = $", first: {count}";
            var skip = offset! > 0 ? $", offset: {offset}" : "";

            var query = Options.CurrentValue.ScopeFullQuery.Replace(partF, $"(func: uid(appId){take}{skip})")
                .Replace(partN, @$"
                appId as var{partF}
                {partN}");

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var result = await txn.Query<TScope>(partN, query);

            foreach (var item in result)
            {
                yield return item;
            }
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
            Func<IQueryable<TScope>, TState, IQueryable<TResult>> query,
            TState state, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var result = await txn.Query<TScope>("oidc_scope", Options.CurrentValue.ScopeFullQuery);

            await foreach (var element in query(result.AsQueryable(), state).ToAsyncEnumerable())
            {
                yield return element;
            }
        }

        /// <inheritdoc/>
        public virtual ValueTask SetDescriptionAsync(TScope scope, string? description, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            scope.Description = description;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetDescriptionsAsync(TScope scope,
            ImmutableDictionary<CultureInfo, string> descriptions, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            scope.Descriptions = descriptions.ToDictionary(k => k.Key, v => v.Value);
            scope.Descriptions.SetPredicate(scope.GetColumnName(x => x.Descriptions));

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetDisplayNamesAsync(TScope scope,
            ImmutableDictionary<CultureInfo, string> names, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            scope.DisplayNames = names.ToDictionary(k => k.Key, v => v.Value);
            scope.DisplayNames.SetPredicate(scope.GetColumnName(x => x.DisplayNames));

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetDisplayNameAsync(TScope scope, string? name, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            scope.DisplayName = name;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetNameAsync(TScope scope, string? name, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            scope.Name = name;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetPropertiesAsync(TScope scope,
            ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (properties is null || properties.IsEmpty)
            {
                scope.Properties = Array.Empty<byte>();

                return default;
            }

            scope.Properties = ByteString.CopyFromUtf8(JsonConvert
                .SerializeObject(properties.ToDictionary(k => k.Key, v => v.Value))).ToByteArray();

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetResourcesAsync(TScope scope, ImmutableArray<string> resources, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (resources.IsDefaultOrEmpty)
            {
                scope.Resources = new();

                return default;
            }

            scope.Resources = resources.ToList();

            return default;
        }

        /// <inheritdoc/>
        public virtual async ValueTask UpdateAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            // Generate a new concurrency token and attach it
            // to the scope before persisting the changes.
            var timestamp = scope.ConcurrencyToken;
            scope.ConcurrencyToken = Guid.NewGuid();

            var database = await Context.GetDatabaseAsync(cancellationToken);

            if (scope.Id.IsEmpty || scope.Id.IsReferenceOnly)
            {
                await CreateAsync(scope, cancellationToken);
                return;
            }

            var type = Options.CurrentValue.ScopeTypeName;

            var req = new Request { CommitNow = true, Query = $@"query Q($id: string, $ct: string) {{ u as var(func: uid($id) AND eq(oidc_concurrency_token, $ct))  @filter(type({type})) }}" };

            req.Vars.Add(new Dictionary<string, string>
            {
                ["id"] = scope.Id,
                ["ct"] = timestamp.ToString()
            });

            var mu = new Mutation
            {
                CommitNow = true,
                Cond = "@if(eq(len(u, 1)))",
                SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(scope, new JsonSerializerSettings
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
                    throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0245));
                }
            }
            catch (Exception e)
            {
                if (e is OpenIddictExceptions.ConcurrencyException)
                    throw;

                throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0245), e);
            }
        }
    }
}
