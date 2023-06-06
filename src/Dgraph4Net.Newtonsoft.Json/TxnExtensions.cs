using System.Reflection;
using Dgraph4Net.ActiveRecords;

namespace Dgraph4Net;

public static class TxnExtensions
{
    static TxnExtensions()
    {
        TxnExts.QueryWithVarsFunc = typeof(TxnExts).GetMethod(nameof(TxnExts.QueryWithVars), BindingFlags.Static | BindingFlags.NonPublic);
        TxnExts.QueryFunc = typeof(TxnExts).GetMethod(nameof(TxnExts.Query), BindingFlags.Static | BindingFlags.NonPublic);
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
