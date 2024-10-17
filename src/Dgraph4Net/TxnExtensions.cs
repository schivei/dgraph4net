using Api;
using Dgraph4Net.ActiveRecords;
using Google.Protobuf;
using Google.Protobuf.Collections;

namespace Dgraph4Net;

public static class TxnExtensions
{
    public static Task<Response> MutateWithQuery(this Txn txn, Mutation mutation, string query, Dictionary<string, string> vars = null)
    {
        var req = new Request { Query = query };
        vars ??= [];

        foreach (var (key, value) in vars)
            req.Vars.Add(key, value);

        req.Mutations.Add(mutation);

        return txn.Do(req);
    }

    public static async Task<List<T>> QueryWithVars<T>(this Txn txn, string param, string query, Dictionary<string, string> vars) where T : new()
    {
        var resp = await txn.QueryWithVars(query, vars).ConfigureAwait(false);

        return resp.Json.FromJson<List<T>>(param);
    }

    public static async Task<List<T>> Query<T>(this Txn txn, string param, string query) where T : new()
    {
        var resp = await txn.Query(query).ConfigureAwait(false);

        return resp.Json.FromJson<List<T>>(param);
    }

    internal static void Resolve(this Uid.UidResolver resolver, MapField<string, string> uids)
    {
        uids.ToList().ForEach(kv =>
        {
            if (Uid.IsValid($"_:{kv.Key}") && Uid.IsValid(kv.Value))
                resolver.Resolve($"_:{kv.Key}", kv.Value);
        });
    }

    public static void Resolve(MapField<string, string> uids) =>
        Uid.s_resolver.Resolve(uids);

    private static Task<Response> SaveData(Txn txn, object? obj, string? query, Dictionary<string, string> vars, bool commitNow = true)
    {
        if (obj is null)
            return null;

        vars ??= [];

        Mutation mutation;

        if (txn.UseNQuads)
        {
            var (set, del) = NQuadsConverter.ToNQuads(obj);

            if (set.Length == 0 && del.Length == 0)
                return null;

            mutation = new Mutation
            {
                CommitNow = true,
                SetNquads = set.Length > 0 ? set : ByteString.Empty,
                DelNquads = del.Length > 0 ? del : ByteString.Empty
            };
        }
        else
        {
            var (set, del) = ClassMapping.ToJsonBS(obj);

            if (set.Length == 0 && del.Length == 0)
                return null;

            mutation = new Mutation
            {
                CommitNow = commitNow,
                SetNquads = set.Length > 0 ? set : ByteString.Empty,
                DelNquads = del.Length > 0 ? del : ByteString.Empty
            };
        }

        var req = new Request
        {
            CommitNow = commitNow
        };

        if (query is not null and { Length: > 0 })
        {
            req.Query = query;

            foreach (var (key, value) in vars)
                req.Vars.Add(key, value);
        }

        req.Mutations.Add(mutation);

        return txn.Do(req);
    }

    public static Task<Response> Save<T>(this Txn txn, IEnumerable<T> entities, string? query = null, Dictionary<string, string> vars = null!, bool commitNow = true) where T : IEntity =>
        SaveData(txn, entities, query, vars, commitNow);

    public static Task<Response> Save<T>(this Txn txn, T entity, string? query = null, Dictionary<string, string> vars = null!, bool commitNow = true) where T : IEntity =>
        SaveData(txn, entity, query, vars, commitNow);
}
