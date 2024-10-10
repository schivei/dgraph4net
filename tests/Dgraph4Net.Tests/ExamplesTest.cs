using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Api;
using Dgraph4Net.ActiveRecords;
using dotenv.net;
using Grpc.Core;
using Grpc.Net.Client;
using Xunit;

namespace Dgraph4Net.Tests;

[Collection("Dgraph4Net")]
public class ExamplesTest : Assert
{
    private static readonly IReadOnlyDictionary<string, string> s_env;

    public static string? GetEnv(string key, string? defaultValue = null) =>
        s_env.TryGetValue(key, out var value) ? value :
            Environment.GetEnvironmentVariable(key) ?? defaultValue;

    static ExamplesTest()
    {
        DotEnv.Load();
        s_env = DotEnv.Read().AsReadOnly();
    }

    public ExamplesTest()
    {
        // just to set Newtonsoft manually
        ClassMapping.ImplClassMapping = new NJClassMapping();

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

        var host = GetEnv("DGRAPH_HOST", "http://localhost:9080");

        var channel = GrpcChannel.ForAddress(host, new GrpcChannelOptions
        {
            Credentials = ChannelCredentials.Insecure
        });

        var dg = new Dgraph4NetClient(channel);

        return dg;
    }

    protected static async Task ClearDB()
    {
        try
        {
            var dg = GetDgraphClient();

            await dg.Alter(new Operation { DropAll = true, AlsoDropDgraphSchema = true, RunInBackground = false });
        }
        catch
        {
            // ignored
        }
    }
}
