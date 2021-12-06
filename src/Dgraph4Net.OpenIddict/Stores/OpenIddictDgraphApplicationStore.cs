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
    /// Provides methods allowing to manage the applications stored in a database.
    /// </summary>
    /// <typeparam name="TApplication">The type of the Application entity.</typeparam>
    public class OpenIddictDgraphApplicationStore<TApplication> : IOpenIddictApplicationStore<TApplication>
        where TApplication : OpenIddictDgraphApplication, IEntity, new()
    {
        public OpenIddictDgraphApplicationStore(
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

            var counts = await txn.Query<CountResult>("oidc_application", $@"{{
                oidc_application(func: type({Options.CurrentValue.ApplicationTypeName})) {{
                    count(uid)
                }}
            }}");

            return counts.FirstOrDefault()?.Count ?? 0;
        }

        /// <inheritdoc/>
        public virtual async ValueTask<long> CountAsync<TResult>(
            Func<IQueryable<TApplication>, IQueryable<TResult>> query, CancellationToken cancellationToken)
        {
            if (query is null)
            {
                return await CountAsync(cancellationToken);
            }

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var applications = await txn.Query<TApplication>("oidc_application", Options.CurrentValue.ApplicationFullQuery);

            return query(applications.AsQueryable()).LongCount();
        }

        /// <inheritdoc/>
        public virtual async ValueTask CreateAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(cancellationToken: cancellationToken);

            var req = new Request { CommitNow = true };

            var type = Options.CurrentValue.ApplicationTypeName;

            var mu = new Mutation
            {
                CommitNow = true,
                SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(application))
            };

            if (application.ClientId is not null)
            {
                var query = $@"query Q($clientId: string) {{ u as var(func: eq({application.GetColumnName("ClientId")}, $clientId))  @filter(type({type})) }}";

                req.Vars.Add(new Dictionary<string, string>
                {
                    ["clientId"] = application.ClientId
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
                    throw new OpenIddictExceptions.ConcurrencyException("Already exists an application with the same 'ClientID'.");
                }

                throw new OpenIddictExceptions.GenericException("One or more errors ocurred when try to create application.");
            }
        }

        /// <inheritdoc/>
        public virtual async ValueTask DeleteAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            var app = await FindByIdAsync(application.Id, cancellationToken) ??
                throw new InvalidDataException($"The application id '{application.Id}' is not valid.");

            application = app!;

            var database = await Context.GetDatabaseAsync(cancellationToken);

            var mu = new Mutation
            {
                CommitNow = true,
                DeleteJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(application))
            };

            using var txn = (Txn)database.NewTransaction(cancellationToken: cancellationToken);

            var response = await txn.Mutate(mu);

            var resp = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Json.ToStringUtf8());

            if (!resp.TryGetValue("code", out var s) || s?.ToString() != "Success")
            {
                throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0239));
            }
        }

        /// <inheritdoc/>
        public virtual async ValueTask<TApplication?> FindByClientIdAsync(string identifier, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
            }

            var part = $"type({Options.CurrentValue.ApplicationTypeName}))";

            var query = "query Q($clientId: string)" + Options.CurrentValue.ApplicationFullQuery
                .Replace(part, $"eq(oidc_client_id, $clientId)) @filter({part}");

            var database = await Context.GetDatabaseAsync(cancellationToken);
            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var result = await txn.QueryWithVars<TApplication>("oidc_application", query, new()
            {
                ["clientId"] = identifier
            });

            return result.Cast<TApplication?>().FirstOrDefault();
        }

        /// <inheritdoc/>
        public virtual async ValueTask<TApplication?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
            }

            var part = $"type({Options.CurrentValue.ApplicationTypeName}))";

            var query = "query Q($appId: string)" + Options.CurrentValue.ApplicationFullQuery
                .Replace(part, $"uid($appId)) @filter({part}");

            var database = await Context.GetDatabaseAsync(cancellationToken);
            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var result = await txn.QueryWithVars<TApplication>("oidc_application", query, new()
            {
                ["appId"] = identifier
            });

            return result.Cast<TApplication?>().FirstOrDefault();
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TApplication> FindByPostLogoutRedirectUriAsync(
            string address, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0143), nameof(address));
            }

            var part = $"type({Options.CurrentValue.ApplicationTypeName}))";

            var query = "query Q($address: string)" + Options.CurrentValue.ApplicationFullQuery
                .Replace(part, $"eq(oidc_post_logout_redirect_uris, $address)) @filter({part}");

            var database = await Context.GetDatabaseAsync(cancellationToken);
            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var result = await txn.QueryWithVars<TApplication>("oidc_application", query, new()
            {
                ["address"] = address
            });

            foreach (var item in result)
            {
                yield return item;
            }
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TApplication> FindByRedirectUriAsync(
            string address, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0143), nameof(address));
            }

            var part = $"type({Options.CurrentValue.ApplicationTypeName}))";

            var query = "query Q($address: string)" + Options.CurrentValue.ApplicationFullQuery
                .Replace(part, $"eq(oidc_redirect_uris, $address)) @filter({part}");

            var database = await Context.GetDatabaseAsync(cancellationToken);
            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var result = await txn.QueryWithVars<TApplication>("oidc_application", query, new()
            {
                ["address"] = address
            });

            foreach (var item in result)
            {
                yield return item;
            }
        }

        /// <inheritdoc/>
        public virtual async ValueTask<TResult> GetAsync<TState, TResult>(
            Func<IQueryable<TApplication>, TState, IQueryable<TResult>> query,
            TState state, CancellationToken cancellationToken)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var applications = await txn.Query<TApplication>("oidc_application", Options.CurrentValue.ApplicationFullQuery);

            return query(applications.AsQueryable(), state).FirstOrDefault() ?? default!;
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetClientIdAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            return new ValueTask<string?>(application.ClientId);
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetClientSecretAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            return new ValueTask<string?>(application.ClientSecret);
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetClientTypeAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            return new ValueTask<string?>(application.Type);
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetConsentTypeAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            return new ValueTask<string?>(application.ConsentType);
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetDisplayNameAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            return new ValueTask<string?>(application.DisplayName);
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (application.DisplayNames is null || application.DisplayNames.Count == 0)
            {
                return new ValueTask<ImmutableDictionary<CultureInfo, string>>(ImmutableDictionary.Create<CultureInfo, string>());
            }

            return new ValueTask<ImmutableDictionary<CultureInfo, string>>(application.DisplayNames
                .Where(x => x.Value is not null)
                .ToImmutableDictionary(k => k.CultureInfo, v => v.Value!));
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetIdAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            return new ValueTask<string?>(application.Id.ToString());
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableArray<string>> GetPermissionsAsync(
            TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (application.Permissions is null || application.Permissions.Count == 0)
            {
                return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
            }

            return new ValueTask<ImmutableArray<string>>(application.Permissions.ToImmutableArray());
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableArray<string>> GetPostLogoutRedirectUrisAsync(
            TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (application.PostLogoutRedirectUris is null || application.PostLogoutRedirectUris.Count == 0)
            {
                return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
            }

            return new ValueTask<ImmutableArray<string>>(application.PostLogoutRedirectUris.ToImmutableArray());
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (application.Properties is null)
            {
                return new ValueTask<ImmutableDictionary<string, JsonElement>>(ImmutableDictionary.Create<string, JsonElement>());
            }

            var document = JsonConvert.DeserializeObject<Dictionary<string, JsonElement>>(ByteString.CopyFrom(application.Properties).ToStringUtf8());

            return new ValueTask<ImmutableDictionary<string, JsonElement>>(document.ToImmutableDictionary());
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableArray<string>> GetRedirectUrisAsync(
            TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (application.RedirectUris is null || application.RedirectUris.Count == 0)
            {
                return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
            }

            return new ValueTask<ImmutableArray<string>>(application.RedirectUris.ToImmutableArray());
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableArray<string>> GetRequirementsAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (application.Requirements is null || application.Requirements.Count == 0)
            {
                return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
            }

            return new ValueTask<ImmutableArray<string>>(application.Requirements.ToImmutableArray());
        }

        /// <inheritdoc/>
        public virtual ValueTask<TApplication> InstantiateAsync(CancellationToken cancellationToken)
        {
            try
            {
                return new ValueTask<TApplication>(Activator.CreateInstance<TApplication>());
            }

            catch (MemberAccessException exception)
            {
                return new ValueTask<TApplication>(Task.FromException<TApplication>(
                    new InvalidOperationException(SR.GetResourceString(SR.ID0240), exception)));
            }
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TApplication> ListAsync(
            int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            count ??= 10;
            offset ??= 0;

            var partN = "oidc_application";
            var partF = $"(func: type({Options.CurrentValue.ApplicationTypeName}))";

            var take = $", first: {count}";
            var skip = offset! > 0 ? $", offset: {offset}" : "";

            var query = Options.CurrentValue.ApplicationFullQuery.Replace(partF, $"(func: uid(appId){take}{skip})")
                .Replace(partN, @$"
                appId as var{partF}
                {partN}");

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var applications = await txn.Query<TApplication>(partN, query);

            foreach (var application in applications)
            {
                yield return application;
            }
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
            Func<IQueryable<TApplication>, TState, IQueryable<TResult>> query,
            TState state, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var database = await Context.GetDatabaseAsync(cancellationToken);

            using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

            var applications = await txn.Query<TApplication>("oidc_application", Options.CurrentValue.ApplicationFullQuery);

            await foreach (var element in query(applications.AsQueryable(), state).ToAsyncEnumerable())
            {
                yield return element;
            }
        }

        /// <inheritdoc/>
        public virtual ValueTask SetClientIdAsync(TApplication application,
            string? identifier, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            application.ClientId = identifier;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetClientSecretAsync(TApplication application,
            string? secret, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            application.ClientSecret = secret;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetClientTypeAsync(TApplication application,
            string? type, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            application.Type = type;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetConsentTypeAsync(TApplication application,
            string? type, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            application.ConsentType = type;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetDisplayNameAsync(TApplication application,
            string? name, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            application.DisplayName = name;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetDisplayNamesAsync(TApplication application,
            ImmutableDictionary<CultureInfo, string> names, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            application.DisplayNames = names.ToDictionary(k => k.Key, v => v.Value);
            application.DisplayNames.SetPredicate(application.GetColumnName(x => x.DisplayNames));

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetPermissionsAsync(TApplication application, ImmutableArray<string> permissions, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (permissions.IsDefaultOrEmpty)
            {
                application.Permissions = new();

                return default;
            }

            application.Permissions = permissions.ToList();

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetPostLogoutRedirectUrisAsync(TApplication application,
            ImmutableArray<string> addresses, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (addresses.IsDefaultOrEmpty)
            {
                application.PostLogoutRedirectUris = new();

                return default;
            }

            application.PostLogoutRedirectUris = addresses.ToList();

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetPropertiesAsync(TApplication application,
            ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (properties is null || properties.IsEmpty)
            {
                application.Properties = Array.Empty<byte>();

                return default;
            }

            application.Properties = ByteString.CopyFromUtf8(JsonConvert
                .SerializeObject(properties.ToDictionary(k => k.Key, v => v.Value))).ToByteArray();

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetRedirectUrisAsync(TApplication application,
            ImmutableArray<string> addresses, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (addresses.IsDefaultOrEmpty)
            {
                application.RedirectUris = new();

                return default;
            }

            application.RedirectUris = addresses.ToList();

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetRequirementsAsync(TApplication application,
            ImmutableArray<string> requirements, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (requirements.IsDefaultOrEmpty)
            {
                application.Requirements = new();

                return default;
            }

            application.Requirements = requirements.ToList();

            return default;
        }

        /// <inheritdoc/>
        public virtual async ValueTask UpdateAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            var timestamp = application.ConcurrencyToken;
            application.ConcurrencyToken = Guid.NewGuid();

            var database = await Context.GetDatabaseAsync(cancellationToken);

            if (application.Id.IsEmpty || application.Id.IsReferenceOnly)
            {
                await CreateAsync(application, cancellationToken);
                return;
            }

            var type = Options.CurrentValue.ApplicationTypeName;

            var req = new Request { CommitNow = true, Query = $@"query Q($id: string, $ct: string) {{ u as var(func: uid($id) AND eq(oidc_concurrency_token, $ct))  @filter(type({type})) }}" };

            req.Vars.Add(new Dictionary<string, string>
            {
                ["id"] = application.Id,
                ["ct"] = timestamp.ToString()
            });

            var mu = new Mutation
            {
                CommitNow = true,
                Cond = "@if(eq(len(u, 1)))",
                SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(application, new JsonSerializerSettings
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
                    throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0239));
                }
            }
            catch (Exception e)
            {
                if (e is OpenIddictExceptions.ConcurrencyException)
                    throw;

                throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0239), e);
            }
        }
    }
}
