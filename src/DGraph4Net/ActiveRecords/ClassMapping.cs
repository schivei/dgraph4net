using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Text;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Dgraph4Net.ActiveRecords;

public static class ClassMapping
{
    internal static ConcurrentDictionary<Type, IClassMap> ClassMappings { get; }

    internal static ConcurrentBag<Migration> Migrations { get; private set; }

    static ClassMapping()
    {
        ClassMappings = new ConcurrentDictionary<Type, IClassMap>();
    }

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

    internal static void Map() =>
        Map(AppDomain.CurrentDomain.GetAssemblies());

    internal static void Map(params Assembly[] assemblies)
    {
        var mappings = assemblies.SelectMany(x =>
        {
            try
            {
                return x.GetTypes();
            }
            catch
            {
                return Array.Empty<Type>();
            }
        })
            .Where(x => x.IsAssignableTo(typeof(IClassMap)) && x.IsClass && !x.IsAbstract)
            .Distinct()
            .Select(ClassMap.CreateInstance)
            .ToList();

        mappings.ForEach(x =>
        {
            x.Start();
            x.Finish();

            ClassMappings.TryAdd(x.Type, x);
        });

        var migrations = assemblies.SelectMany(x =>
        {
            try
            {
                return x.GetTypes();
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }).Where(x => x.IsAssignableTo(typeof(Migration)) && x.IsClass && !x.IsAbstract)
        .Select(x => (Migration)Activator.CreateInstance(x))
        .ToList();

        Migrations = new(migrations);
    }

    internal static string CreateScript()
    {
        var predicates = ClassMap.Predicates.Values
            .Where(x => x is not UidPredicate and not TypePredicate)
            .GroupBy(x => x.PredicateName)
            .Where(x => !string.IsNullOrEmpty(x.Key))
            .Select(x => (x.Key, p: x.Aggregate((p1, p2) => p1.Merge(p2))))
            .Select(x => (x.Key, p: x.p.ToSchemaPredicate()))
            .Where(x => !string.IsNullOrEmpty(x.p))
            .OrderBy(x => x.Key)
            .Aggregate(new StringBuilder(), (sb, x) => sb.AppendLine(x.p), sb => sb.ToString());

        var types = ClassMappings.Values
            .Select(x => (classMap: x, predicates: ClassMap.Predicates.Values.Where(y => y.ClassMap == x).ToArray()))
            .DistinctBy(x => x.classMap.DgraphType)
            .OrderBy(x => x.classMap.DgraphType)
            .Aggregate(new StringBuilder(), (sb, type) =>
                sb.Append("type ")
                  .Append(type.classMap.DgraphType)
                  .AppendLine(" {")
                  .AppendJoin('\n', type.predicates
                      .Where(x => x is not UidPredicate and not TypePredicate)
                      .GroupBy(x => x.PredicateName)
                      .Where(x => !string.IsNullOrEmpty(x.Key))
                      .AsParallel()
                      .Select(x => (x.Key, p: x.Aggregate((p1, p2) => p1.Merge(p2))))
                      .Select(x => (x.Key, p: x.p.ToTypePredicate()))
                      .Where(x => !string.IsNullOrEmpty(x.p))
                      .ToArray()
                      .OrderBy(x => x.Key)
                      .Select(x => $"  {x.p}"))
                  .AppendLine()
                  .AppendLine("}")
                  .AppendLine()
            , sb => sb.ToString().Replace("\r\n", "\n").Replace("\r", "\n").Trim('\n') + "\n");

        return new StringBuilder().AppendLine(predicates).AppendLine(types).ToString()
            .Replace("\r\n", "\n").Replace("\r", "\n");
    }

    public static string GetDgraphType(Type type)
    {
        if (ClassMappings.TryGetValue(type, out var classMap))
            return classMap.DgraphType;

        return type.Name;
    }

    public static string GetDgraphType<T>() =>
        GetDgraphType(typeof(T));
}
