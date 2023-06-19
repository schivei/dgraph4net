using System.Reflection;
using Api;
using Dgraph4Net.ActiveRecords;

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
}
