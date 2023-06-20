using System.Reflection;
using Api;
using Dgraph4Net.ActiveRecords;
using Google.Protobuf.Collections;
using Grpc.Net.Client.Balancer;

namespace Dgraph4Net;

public static class TxnExtensions
{
    public static Task<Response> MutateWithQuery(this Txn txn, Mutation mutation, string query, Dictionary<string, string> vars = null)
    {
        var req = new Request { Query = query };
        vars ??= new Dictionary<string, string>();

        foreach (var (key, value) in vars)
            req.Vars.Add(key, value);

        req.Mutations.Add(mutation);

        return txn.Do(req);
    }

    public static async Task<List<T>> QueryWithVars<T>(this Txn txn, string param, string query, Dictionary<string, string> vars) where T : class, IEntity, new()
    {
        var resp = await txn.QueryWithVars(query, vars).ConfigureAwait(false);

        return resp.Json.FromJson<List<T>>(param);
    }

    public static async Task<List<T>> Query<T>(this Txn txn, string param, string query) where T : class, IEntity, new()
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
}
