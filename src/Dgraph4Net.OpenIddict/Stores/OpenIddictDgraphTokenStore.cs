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

using SR = OpenIddict.Abstractions.OpenIddictResources;
using System.Text.Json.Serialization;
using Api;
using Google.Protobuf;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Dgraph4Net.OpenIddict.Stores;

/// <summary>
/// Provides methods allowing to manage the tokens stored in a database.
/// </summary>
/// <typeparam name="TToken">The type of the Token entity.</typeparam>
public class OpenIddictDgraphTokenStore<TToken> : IOpenIddictTokenStore<TToken>
    where TToken : OpenIddictDgraphToken, IEntity, new()
{
    public OpenIddictDgraphTokenStore(
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

        var counts = await txn.Query<CountResult>("oidc_token", $@"{{
                oidc_token(func: type({Options.CurrentValue.TokenTypeName})) {{
                    count(uid)
                }}
            }}");

        return counts.FirstOrDefault()?.Count ?? 0;
    }

    /// <inheritdoc/>
    public virtual async ValueTask<long> CountAsync<TResult>(
        Func<IQueryable<TToken>, IQueryable<TResult>> query, CancellationToken cancellationToken)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var database = await Context.GetDatabaseAsync(cancellationToken);

        using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

        var scopes = await txn.Query<TToken>("oidc_token", Options.CurrentValue.TokenFullQuery);

        return query(scopes.AsQueryable()).LongCount();
    }

    /// <inheritdoc/>
    public virtual async ValueTask CreateAsync(TToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        var database = await Context.GetDatabaseAsync(cancellationToken);

        using var txn = (Txn)database.NewTransaction(cancellationToken: cancellationToken);

        var req = new Request { CommitNow = true };

        var type = Options.CurrentValue.TokenTypeName;

        var mu = new Mutation
        {
            CommitNow = true,
            SetJson = ByteString.CopyFromUtf8(JsonSerializer.Serialize(token))
        };

        if (token.ReferenceId is not null)
        {
            var query = $@"query Q($name: string) {{ u as var(func: eq({token.GetColumnName(t => t.ReferenceId)}, $name))  @filter(type({type})) }}";

            req.Vars.Add(new Dictionary<string, string>
            {
                ["name"] = token.ReferenceId
            });

            mu.Cond = "@if(eq(len(u, 1)))";
        }

        req.Mutations.Add(mu);

        var response = await txn.Do(req);

        if (response.Uids.Count == 0)
        {
            var resp = JsonSerializer.Deserialize<Dictionary<string, object>>(response.Json.ToStringUtf8());

            if (!resp.TryGetValue("code", out var s) || s?.ToString() != "Success")
            {
                throw new OpenIddictExceptions.ConcurrencyException("Already exists an token with the same 'ReferenceId'.");
            }

            throw new OpenIddictExceptions.ValidationException("One or more errors ocurred when try to create token.");
        }
    }

    /// <inheritdoc/>
    public virtual async ValueTask DeleteAsync(TToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        if (token.Id.IsEmpty || token.Id.IsReferenceOnly)
            throw new InvalidDataException($"The token id '{token.Id}' is not valid.");

        var database = await Context.GetDatabaseAsync(cancellationToken);

        var mu = new Mutation
        {
            CommitNow = true,
            DelNquads = ByteString.CopyFromUtf8($"uid({token.Id}) * * .")
        };

        using var txn = (Txn)database.NewTransaction(cancellationToken: cancellationToken);

        var response = await txn.Mutate(mu);

        var resp = JsonSerializer.Deserialize<Dictionary<string, object>>(response.Json.ToStringUtf8());

        if (!resp.TryGetValue("code", out var s) || s?.ToString() != "Success")
        {
            throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0247));
        }
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<TToken> FindAsync(string subject,
        string client, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(subject))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0198), nameof(subject));
        }

        if (string.IsNullOrEmpty(client))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0124), nameof(client));
        }

        var part = $"type({Options.CurrentValue.TokenTypeName}))";

        var tok = typeof(TToken).As<TToken>();
        var app = typeof(OpenIddictDgraphApplication).As<OpenIddictDgraphApplication>();

        var sbf = tok.GetColumnName(f => f.Subject);
        var apf = tok.GetColumnName(f => f.ApplicationId);
        var clf = app.GetColumnName(f => f.ClientId);

        var clientIsApp = Uid.IsValid(client);

        var apFilter = clientIsApp ? $"uid($client)" : $"eq({clf}, $client)";

        var query = "query Q($client: string, $subject: string)" + Options.CurrentValue.TokenFullQuery
            .Replace(part, $"eq({sbf}, $subject)) @cascade @filter({part}")
            .Replace($"{apf}", $"{apf} @filter({apFilter})");

        var database = await Context.GetDatabaseAsync(cancellationToken);

        using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

        var result = await txn.QueryWithVars<TToken>("oidc_token", query, new()
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
    public virtual async IAsyncEnumerable<TToken> FindAsync(
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

        var part = $"type({Options.CurrentValue.TokenTypeName}))";

        var tok = typeof(TToken).As<TToken>();
        var app = typeof(OpenIddictDgraphApplication).As<OpenIddictDgraphApplication>();

        var sbf = tok.GetColumnName(f => f.Subject);
        var stf = tok.GetColumnName(f => f.Status);
        var apf = tok.GetColumnName(f => f.ApplicationId);
        var clf = app.GetColumnName(f => f.ClientId);

        var clientIsApp = Uid.IsValid(client);

        var apFilter = clientIsApp ? $"uid($client)" : $"eq({clf}, $client)";

        var query = "query Q($client: string, $subject: string, $status: string)" + Options.CurrentValue.TokenFullQuery
            .Replace(part, $"eq({sbf}, $subject) AND eq({stf}, $status)) @cascade @filter({part}")
            .Replace($"{apf}", $"{apf} @filter({apFilter})");

        var database = await Context.GetDatabaseAsync(cancellationToken);

        using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

        var result = await txn.QueryWithVars<TToken>("oidc_token", query, new()
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
    public virtual async IAsyncEnumerable<TToken> FindAsync(
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

        var part = $"type({Options.CurrentValue.TokenTypeName}))";

        var tok = typeof(TToken).As<TToken>();
        var app = typeof(OpenIddictDgraphApplication).As<OpenIddictDgraphApplication>();

        var sbf = tok.GetColumnName(f => f.Subject);
        var stf = tok.GetColumnName(f => f.Status);
        var apf = tok.GetColumnName(f => f.ApplicationId);
        var clf = app.GetColumnName(f => f.ClientId);
        var tpf = tok.GetColumnName(f => f.Type);

        var clientIsApp = Uid.IsValid(client);

        var apFilter = clientIsApp ? $"uid($client)" : $"eq({clf}, $client)";

        var query = "query Q($client: string, $subject: string, $status: string, $type: string)" + Options.CurrentValue.TokenFullQuery
            .Replace(part, $"eq({sbf}, $subject) AND eq({stf}, $status) AND eq({tpf}, $type)) @cascade @filter({part}")
            .Replace($"{apf}", $"{apf} @filter({apFilter})");

        var database = await Context.GetDatabaseAsync(cancellationToken);

        using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

        var result = await txn.QueryWithVars<TToken>("oidc_token", query, new()
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
    public virtual async IAsyncEnumerable<TToken> FindByApplicationIdAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
        }

        var part = $"type({Options.CurrentValue.TokenTypeName}))";

        var ts = typeof(TToken);

        var query = "query Q($ref: string)" + Options.CurrentValue.TokenFullQuery
            .Replace(part, $"eq({ts.As<TToken>().GetColumnName(c => c.ApplicationId)}, $ref)) @filter({part}");

        var database = await Context.GetDatabaseAsync(cancellationToken);

        using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

        var result = await txn.QueryWithVars<TToken>("oidc_token", query, new()
        {
            ["ref"] = identifier
        });

        foreach (var item in result)
        {
            yield return item;
        }
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<TToken> FindByAuthorizationIdAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
        }

        var part = $"type({Options.CurrentValue.TokenTypeName}))";

        var ts = typeof(TToken);

        var query = "query Q($ref: string)" + Options.CurrentValue.TokenFullQuery
            .Replace(part, $"eq({ts.As<TToken>().GetColumnName(c => c.AuthorizationId)}, $ref)) @filter({part}");

        var database = await Context.GetDatabaseAsync(cancellationToken);

        using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

        var result = await txn.QueryWithVars<TToken>("oidc_token", query, new()
        {
            ["ref"] = identifier
        });

        foreach (var item in result)
        {
            yield return item;
        }
    }

    /// <inheritdoc/>
    public virtual async ValueTask<TToken?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
        }

        var part = $"type({Options.CurrentValue.TokenTypeName}))";

        var query = "query Q($id: string)" + Options.CurrentValue.TokenFullQuery
            .Replace(part, $"uid($id)) @filter({part}");

        var database = await Context.GetDatabaseAsync(cancellationToken);
        using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

        var result = await txn.QueryWithVars<TToken>("oidc_token", query, new()
        {
            ["id"] = identifier
        });

        foreach (var item in result)
        {
            return item;
        }

        return null;
    }

    /// <inheritdoc/>
    public virtual async ValueTask<TToken?> FindByReferenceIdAsync(string identifier, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
        }

        var part = $"type({Options.CurrentValue.TokenTypeName}))";

        var ts = typeof(TToken);

        var query = "query Q($ref: string)" + Options.CurrentValue.TokenFullQuery
            .Replace(part, $"eq({ts.As<TToken>().GetColumnName(c => c.ReferenceId)}, $ref)) @filter({part}");

        var database = await Context.GetDatabaseAsync(cancellationToken);

        using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

        var result = await txn.QueryWithVars<TToken>("oidc_token", query, new()
        {
            ["ref"] = identifier
        });

        foreach (var item in result)
        {
            return item;
        }

        return null;
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<TToken> FindBySubjectAsync(string subject, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(subject))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0198), nameof(subject));
        }

        var part = $"type({Options.CurrentValue.TokenTypeName}))";

        var ts = typeof(TToken);

        var query = "query Q($subject: string)" + Options.CurrentValue.TokenFullQuery
            .Replace(part, $"eq({ts.As<TToken>().GetColumnName(c => c.Subject)}, $subject)) @filter({part}");

        var database = await Context.GetDatabaseAsync(cancellationToken);

        using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

        var result = await txn.QueryWithVars<TToken>("oidc_token", query, new()
        {
            ["subject"] = subject
        });

        foreach (var item in result)
        {
            yield return item;
        }
    }

    /// <inheritdoc/>
    public virtual ValueTask<string?> GetApplicationIdAsync(TToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        if (token.ApplicationId.IsEmpty)
        {
            return new ValueTask<string?>(result: null);
        }

        return new ValueTask<string?>(token.ApplicationId.ToString());
    }

    /// <inheritdoc/>
    public virtual async ValueTask<TResult> GetAsync<TState, TResult>(
        Func<IQueryable<TToken>, TState, IQueryable<TResult>> query,
        TState state, CancellationToken cancellationToken)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var database = await Context.GetDatabaseAsync(cancellationToken);

        using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

        var scopes = await txn.Query<TToken>("oidc_token", Options.CurrentValue.TokenFullQuery);

        return query(scopes.AsQueryable(), state).FirstOrDefault() ?? default!;
    }

    /// <inheritdoc/>
    public virtual ValueTask<string?> GetAuthorizationIdAsync(TToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        if (token.AuthorizationId.IsEmpty)
        {
            return new ValueTask<string?>(result: null);
        }

        return new ValueTask<string?>(token.AuthorizationId.ToString());
    }

    /// <inheritdoc/>
    public virtual ValueTask<DateTimeOffset?> GetCreationDateAsync(TToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        if (token.CreationDate is null)
        {
            return new ValueTask<DateTimeOffset?>(result: null);
        }

        return new ValueTask<DateTimeOffset?>(DateTime.SpecifyKind(token.CreationDate.Value, DateTimeKind.Utc));
    }

    /// <inheritdoc/>
    public virtual ValueTask<DateTimeOffset?> GetExpirationDateAsync(TToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        if (token.ExpirationDate is null)
        {
            return new ValueTask<DateTimeOffset?>(result: null);
        }

        return new ValueTask<DateTimeOffset?>(DateTime.SpecifyKind(token.ExpirationDate.Value, DateTimeKind.Utc));
    }

    /// <inheritdoc/>
    public virtual ValueTask<string?> GetIdAsync(TToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        return new ValueTask<string?>(token.Id.ToString());
    }

    /// <inheritdoc/>
    public virtual ValueTask<string?> GetPayloadAsync(TToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        return new ValueTask<string?>(token.Payload);
    }

    /// <inheritdoc/>
    public virtual ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        if (token.Properties is null)
        {
            return new ValueTask<ImmutableDictionary<string, JsonElement>>(ImmutableDictionary.Create<string, JsonElement>());
        }

        var document = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ByteString.CopyFrom(token.Properties).ToStringUtf8());

        return new ValueTask<ImmutableDictionary<string, JsonElement>>(document.ToImmutableDictionary());
    }

    /// <inheritdoc/>
    public virtual ValueTask<DateTimeOffset?> GetRedemptionDateAsync(TToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        if (token.RedemptionDate is null)
        {
            return new ValueTask<DateTimeOffset?>(result: null);
        }

        return new ValueTask<DateTimeOffset?>(DateTime.SpecifyKind(token.RedemptionDate.Value, DateTimeKind.Utc));
    }

    /// <inheritdoc/>
    public virtual ValueTask<string?> GetReferenceIdAsync(TToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        return new ValueTask<string?>(token.ReferenceId);
    }

    /// <inheritdoc/>
    public virtual ValueTask<string?> GetStatusAsync(TToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        return new ValueTask<string?>(token.Status);
    }

    /// <inheritdoc/>
    public virtual ValueTask<string?> GetSubjectAsync(TToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        return new ValueTask<string?>(token.Subject);
    }

    /// <inheritdoc/>
    public virtual ValueTask<string?> GetTypeAsync(TToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        return new ValueTask<string?>(token.Type);
    }

    /// <inheritdoc/>
    public virtual ValueTask<TToken> InstantiateAsync(CancellationToken cancellationToken)
    {
        try
        {
            return new ValueTask<TToken>(Activator.CreateInstance<TToken>());
        }

        catch (MemberAccessException exception)
        {
            return new ValueTask<TToken>(Task.FromException<TToken>(
                new InvalidOperationException(SR.GetResourceString(SR.ID0248), exception)));
        }
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<TToken> ListAsync(
        int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        count ??= 10;
        offset ??= 0;

        var partN = "oidc_token";
        var partF = $"(func: type({Options.CurrentValue.TokenTypeName}))";

        var take = $", first: {count}";
        var skip = offset! > 0 ? $", offset: {offset}" : "";

        var query = Options.CurrentValue.TokenFullQuery.Replace(partF, $"(func: uid(appId){take}{skip})")
            .Replace(partN, @$"
                appId as var{partF}
                {partN}");

        var database = await Context.GetDatabaseAsync(cancellationToken);

        using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

        var result = await txn.Query<TToken>(partN, query);

        foreach (var item in result)
        {
            yield return item;
        }
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
        Func<IQueryable<TToken>, TState, IQueryable<TResult>> query,
        TState state, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var database = await Context.GetDatabaseAsync(cancellationToken);

        using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

        var result = await txn.Query<TToken>("oidc_token", Options.CurrentValue.TokenFullQuery);

        await foreach (var element in query(result.AsQueryable(), state).ToAsyncEnumerable())
        {
            yield return element;
        }
    }

    /// <inheritdoc/>
    public virtual async ValueTask PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        var proxy = typeof(TToken).As<TToken>();
        var cdf = proxy.GetColumnName(x => x.CreationDate);
        var stf = proxy.GetColumnName(x => x.Status);
        var edf = proxy.GetColumnName(x => x.ExpirationDate);
        var atf = proxy.GetColumnName(x => x.AuthorizationId);

        var type = Options.CurrentValue.TokenTypeName;

        var query = $@"query Q($time: string, $inactive: string, $valid: string, $now: string) {{
              token as var(func: lt({cdf}, $time))
                @filter(type({type})
                    AND (NOT eq({stf}, $inactive) AND NOT eq({stf}, $valid))
                     OR lt({edf}, $now)
                     OR (has({atf}) AND NOT eq({atf}.{stf}, $valid)))
            }}";

        var mu = new Mutation
        {
            CommitNow = true,
            Cond = "@if(gt(len(token), 0))",
            DelNquads = ByteString.CopyFromUtf8("uid(token) * * .")
        };

        var req = new Request
        {
            CommitNow = true,
            Query = query
        };

        req.Mutations.Add(mu);

        req.Vars.Add(new Dictionary<string, string>
        {
            ["time"] = threshold.UtcDateTime.ToString("O"),
            ["inactive"] = Statuses.Inactive,
            ["valid"] = Statuses.Valid,
            ["now"] = DateTime.UtcNow.ToString("O")
        });

        var database = await Context.GetDatabaseAsync(cancellationToken);

        using var txn = (Txn)database.NewTransaction(true, true, cancellationToken);

        var response = await txn.Do(req);

        var resp = JsonSerializer.Deserialize<Dictionary<string, object>>(response.Json.ToStringUtf8());

        if (!resp.TryGetValue("code", out var s) || s?.ToString() != "Success")
        {
            throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0241));
        }
    }

    /// <inheritdoc/>
    public virtual ValueTask SetApplicationIdAsync(TToken token, string? identifier, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        if (!string.IsNullOrEmpty(identifier))
        {
            token.ApplicationId = identifier!;
        }

        else
        {
            token.ApplicationId = default!;
        }

        return default;
    }

    /// <inheritdoc/>
    public virtual ValueTask SetAuthorizationIdAsync(TToken token, string? identifier, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        if (!string.IsNullOrEmpty(identifier))
        {
            token.AuthorizationId = identifier!;
        }
        else
        {
            token.AuthorizationId = default!;
        }

        return default;
    }

    /// <inheritdoc/>
    public virtual ValueTask SetCreationDateAsync(TToken token, DateTimeOffset? date, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        token.CreationDate = date?.UtcDateTime;

        return default;
    }

    /// <inheritdoc/>
    public virtual ValueTask SetExpirationDateAsync(TToken token, DateTimeOffset? date, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        token.ExpirationDate = date?.UtcDateTime;

        return default;
    }

    /// <inheritdoc/>
    public virtual ValueTask SetPayloadAsync(TToken token, string? payload, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        token.Payload = payload;

        return default;
    }

    /// <inheritdoc/>
    public virtual ValueTask SetPropertiesAsync(TToken token,
        ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        if (properties is null || properties.IsEmpty)
        {
            token.Properties = Array.Empty<byte>();

            return default;
        }

        token.Properties = ByteString.CopyFromUtf8(JsonSerializer.Serialize(properties.ToDictionary(k => k.Key, v => v.Value))).ToByteArray();

        return default;
    }

    /// <inheritdoc/>
    public virtual ValueTask SetRedemptionDateAsync(TToken token, DateTimeOffset? date, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        token.RedemptionDate = date?.UtcDateTime;

        return default;
    }

    /// <inheritdoc/>
    public virtual ValueTask SetReferenceIdAsync(TToken token, string? identifier, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        token.ReferenceId = identifier;

        return default;
    }

    /// <inheritdoc/>
    public virtual ValueTask SetStatusAsync(TToken token, string? status, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        token.Status = status;

        return default;
    }

    /// <inheritdoc/>
    public virtual ValueTask SetSubjectAsync(TToken token, string? subject, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        token.Subject = subject;

        return default;
    }

    /// <inheritdoc/>
    public virtual ValueTask SetTypeAsync(TToken token, string? type, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        token.Type = type;

        return default;
    }

    /// <inheritdoc/>
    public virtual async ValueTask UpdateAsync(TToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        // Generate a new concurrency token and attach it
        // to the token before persisting the changes.
        var timestamp = token.ConcurrencyToken;
        token.ConcurrencyToken = Guid.NewGuid();

        var database = await Context.GetDatabaseAsync(cancellationToken);

        if (token.Id.IsEmpty || token.Id.IsReferenceOnly)
        {
            await CreateAsync(token, cancellationToken);
            return;
        }

        var type = Options.CurrentValue.TokenTypeName;

        var req = new Request { CommitNow = true, Query = $@"query Q($id: string, $ct: string) {{ u as var(func: uid($id) AND eq(oidc_concurrency_token, $ct))  @filter(type({type})) }}" };

        req.Vars.Add(new Dictionary<string, string>
        {
            ["id"] = token.Id,
            ["ct"] = timestamp.ToString()
        });

        var mu = new Mutation
        {
            CommitNow = true,
            Cond = "@if(eq(len(u, 1)))",
            SetJson = ByteString.CopyFromUtf8(JsonSerializer.Serialize(token))
        };

        req.Mutations.Add(mu);

        using var txn = (Txn)database.NewTransaction(cancellationToken: cancellationToken);

        try
        {
            var response = await txn.Do(req);

            var resp = JsonSerializer.Deserialize<Dictionary<string, object>>(response.Json.ToStringUtf8());

            if (!resp.TryGetValue("code", out var s) || s?.ToString() != "Success")
            {
                throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0247));
            }
        }
        catch (Exception e)
        {
            if (e is OpenIddictExceptions.ConcurrencyException)
                throw;

            throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0247), e);
        }
    }
}
