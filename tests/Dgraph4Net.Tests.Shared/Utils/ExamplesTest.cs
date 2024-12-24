using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dgraph4Net.ActiveRecords;
using dotenv.net;
using Grpc.Core;
using Grpc.Net.Client;

namespace Dgraph4Net.Tests;

[Collection("Dgraph4Net")]
public abstract class ExamplesTest : Assert
{
    private static readonly IReadOnlyDictionary<string, string> s_env;

    public static string? GetEnv(string key, string? defaultValue = null) =>
        s_env.TryGetValue(key, out var value) ? value :
            Environment.GetEnvironmentVariable(key) ?? defaultValue;

    static ExamplesTest()
    {
        DotEnv.Load();
        s_env = DotEnv.Read().AsReadOnly();

#if NJSON
        ClassMapping.ImplClassMapping = new NjClassMapping();
#endif
    }

    protected ExamplesTest()
    {
        if (GetEnv("GRPC_DNS_RESOLVER") != "native")
        {
            Environment.SetEnvironmentVariable("GRPC_DNS_RESOLVER", "native");
        }

        ClassMapping.ImplClassMapping.SetDefaults();

        try
        {
            ClassMapping.Map();
        }
        catch
        {
            // ignored
        }
    }

    protected static Dgraph4NetClient GetDgraphClient()
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        var host = GetEnv("DGRAPH_HOST", "http://localhost:9080")!;

        var channel = GrpcChannel.ForAddress(host, new()
        {
            Credentials = ChannelCredentials.Insecure
        });

        var dg = new Dgraph4NetClient(channel);

        return dg;
    }

    protected static async Task ClearDb()
    {
        try
        {
            var dg = GetDgraphClient();

            await dg.Alter(new() { DropAll = true, AlsoDropDgraphSchema = true, RunInBackground = false });
        }
        catch
        {
            // ignored
        }
    }
}
