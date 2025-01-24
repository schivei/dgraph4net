using Api;
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
