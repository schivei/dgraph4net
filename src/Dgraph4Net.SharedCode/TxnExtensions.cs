using System.Reflection;
using Google.Protobuf;
using Api;

using Dgraph4Net.ActiveRecords;

namespace Dgraph4Net;

public static class TxnExtension
{
    public static string ToGeoJson(this NetGeo.Json.GeoObject geo)
    {
        var type = geo.GetType();

        // ReSharper disable once JoinDeclarationAndInitializer
        MethodInfo toGeoJson;

#if !NJ
        toGeoJson = typeof(NetGeo.Json.SystemText.GeoExtensions).GetMethod(nameof(NetGeo.Json.SystemText.GeoExtensions.ToGeoJson)).MakeGenericMethod(type);
#else
        toGeoJson = typeof(NetGeo.Json.GeoExtensions).GetMethod(nameof(NetGeo.Json.GeoExtensions.ToGeoJson)).MakeGenericMethod(type);
#endif

        return toGeoJson.Invoke(null, parameters: null)?.ToString();
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

    private static Task<Response> SaveData(Txn txn, object? obj, string? query, Dictionary<string, string> vars, bool commitNow = true)
    {
        if (obj is null)
            return null;

        Mutation mutation;

        if (txn.UseNQuads)
        {
            var (set, del) = NQuadsConverter.ToNQuads(obj);

            if (set.Length == 0 && del.Length == 0)
                return null;

            mutation = new()
            {
                CommitNow = true,
                SetNquads = set.Length > 0 ? set : ByteString.Empty,
                DelNquads = del.Length > 0 ? del : ByteString.Empty
            };
        }
        else
        {
            var (set, del) = ClassMapping.ToJsonBs(obj);

            if (set.Length == 0 && del.Length == 0)
                return null;

            mutation = new()
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

        if (query is { Length: > 0 })
        {
            req.Query = query;

            foreach (var (key, value) in vars)
                req.Vars.Add(key, value);
        }

        req.Mutations.Add(mutation);

        return txn.Do(req);
    }

    public static Task<Response> Save<T>(this Txn txn, IEnumerable<T> entities, string? query = null, Dictionary<string, string> vars = null!, bool commitNow = true) where T : IEntityBase =>
        SaveData(txn, entities, query, vars, commitNow);

    public static Task<Response> Save<T>(this Txn txn, T entity, string? query = null, Dictionary<string, string> vars = null!, bool commitNow = true) where T : IEntityBase =>
        SaveData(txn, entity, query, vars, commitNow);
}
