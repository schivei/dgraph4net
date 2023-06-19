using System.Collections.Concurrent;
using System.Reflection;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dgraph4Net.ActiveRecords;

public static class ClassMapping
{
    private static IClassMapping? s_classMapping;

    internal static IClassMapping ImplClassMapping
    {
        get => s_classMapping ?? new ClassMappingImpl();
        set => s_classMapping = value;
    }

    internal static ConcurrentDictionary<Type, IClassMap> ClassMappings => ImplClassMapping.ClassMappings;

    internal static ConcurrentBag<Migration> Migrations
    {
        get => ImplClassMapping.Migrations;
        private set => ImplClassMapping.Migrations = value;
    }

    public static string ToJsonString<T>(this T entity, bool deep = false, bool doNotPropagateNulls = false) where T : IEntity =>
        ImplClassMapping.ToJsonString<T>(entity, deep, doNotPropagateNulls);

    public static ByteString ToJson<T>(this T entity, bool deep = false, bool doNotPropagateNulls = false) where T : IEntity =>
        ImplClassMapping.ToJson<T>(entity, deep, doNotPropagateNulls);

    private static string ToJson<T>(this T entity, HashSet<IEntity> mapped, bool deep, bool doNotPropagateNulls = false) where T : IEntity =>
        ImplClassMapping.ToJson<T>(entity, mapped, deep, doNotPropagateNulls);

    public static object? FromJson(this ByteString bytes, Type type, string param) =>
        ImplClassMapping.FromJson(bytes, type, param);

    public static T? FromJson<T>(this ByteString bytes, string param) =>
        ImplClassMapping.FromJson<T>(bytes, param);

    /// <summary>
    /// Inverse of <see cref="ToJson{T}(T, Dictionary{Uid, object}, bool, bool)"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bytes"></param>
    /// <param name="loaded"></param>
    /// <returns>DB Result</returns>
    private static object? FromJsonBS(this ByteString bytes, Type type, string param) =>
        ImplClassMapping.FromJsonBS(bytes, type, param);

    public static object? FromJson(this string str, Type type) =>
        ImplClassMapping.FromJson(str, type);

    public static T? FromJson<T>(this string str) =>
        ImplClassMapping.FromJson<T>(str);

    public static object? FromJson(this string str, Type type, string param) =>
        ImplClassMapping.FromJson(str, type, param);

    public static T? FromJson<T>(this string str, string param) =>
        ImplClassMapping.FromJson<T>(str, param);

    public static object? FromJson(this ByteString bytes, Type type) =>
        ImplClassMapping.FromJson(bytes, type);

    public static T? FromJson<T>(this ByteString bytes) =>
        ImplClassMapping.FromJson<T>(bytes);

    private static object? FromJson(this ByteString bytes, Type type, Dictionary<Uid, object> loaded) =>
        ImplClassMapping.FromJson(bytes, type, loaded);

    private static bool TryMapJson(Type type, out IClassMap? classMap) =>
        ImplClassMapping.TryMapJson(type, out classMap);

    public static IServiceCollection AddDgraph(this IServiceCollection services) =>
        services.AddDgraph("DefaultConnection");

    public static IServiceCollection AddDgraph(this IServiceCollection services, string connectionName) =>
        services.AddDgraph(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString(connectionName));

    public static IServiceCollection AddDgraph(this IServiceCollection services, Func<IServiceProvider, string> getConnectionString)
    {
        services.AddTransient<ChannelBase>(sp =>
        {
            var connectionString = getConnectionString(sp);

            var server = Array.Find(connectionString
                .Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                s => s.StartsWith("server=", StringComparison.InvariantCultureIgnoreCase))?
                .Split('=')[1] ?? "localhost:9080";

            var useTls = Array.Find(connectionString
                               .Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                                              s => s.StartsWith("use tls=", StringComparison.InvariantCultureIgnoreCase))?
                .Split('=')[1].ToLowerInvariant() ?? "true";

            return useTls == "false" ?
                new Channel(server, ChannelCredentials.Insecure) :
                new Channel(server, ChannelCredentials.SecureSsl);
        });

        services.AddTransient(sp =>
        {
            var connectionString = getConnectionString(sp);

            var userId = Array.Find(connectionString
                               .Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                                    s => s.StartsWith("user id=", StringComparison.InvariantCultureIgnoreCase))?
                .Split('=')[1];

            var password = Array.Find(connectionString
                                              .Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                                                                                           s => s.StartsWith("password=", StringComparison.InvariantCultureIgnoreCase))?
                .Split('=')[1];

            var client = new Dgraph4NetClient(sp.GetRequiredService<ChannelBase>());

            if (userId is not null)
                client.Login(userId, password);

            return client;
        });

        Map();

        return services;
    }

    public static void Map() =>
        ImplClassMapping.Map();

    public static void Map(params Assembly[] assemblies) =>
        ImplClassMapping.Map(assemblies);

    internal static Task EnsureAsync(Dgraph4NetClient client) =>
        InternalClassMapping.EnsureAsync(client);

    public static string GetDgraphType(Type type) =>
        InternalClassMapping.GetDgraphType(type);

    internal static string CreateScript() =>
        InternalClassMapping.CreateScript();

    public static List<IPredicate> GetPredicates(Type type) =>
        InternalClassMapping.GetPredicates(type);

    public static List<IPredicate> GetPredicates<T>() =>
        InternalClassMapping.GetPredicates(typeof(T));

    public static IPredicate GetPredicate(PropertyInfo prop) =>
        InternalClassMapping.GetPredicate(prop);
}
