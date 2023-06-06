using System.Reflection;

namespace Dgraph4Net;

internal static class TxnExts
{
    internal static MethodInfo QueryWithVarsFunc { get; set; }
    internal static MethodInfo QueryFunc { get; set; }

    public static Task<List<T>> QueryWithVars<T>(this Txn txn, string param, string query, Dictionary<string, string> vars) where T : class, IEntity, new() =>
        QueryWithVarsFunc.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { txn, param, query, vars }) as Task<List<T>>;

    public static Task<List<T>> Query<T>(this Txn txn, string param, string query) where T : class, IEntity, new() =>
        QueryFunc.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { txn, param, query }) as Task<List<T>>;
}
