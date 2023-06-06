using Api;

namespace Dgraph4Net;

public static class TxnsExtensions
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
}
