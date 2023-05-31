using System;
using System.Threading;
using System.Threading.Tasks;

using Api;
using Dgraph4Net.ActiveRecords;
using Grpc.Core;
using Grpc.Net.Client;

using Xunit;

namespace Dgraph4Net.Tests;

[Collection("Dgraph4Net")]
public class ExamplesTest : Assert
{
    public ExamplesTest()
    {
        if (Environment.GetEnvironmentVariable("GRPC_DNS_RESOLVER") != "native")
        {
            Environment.SetEnvironmentVariable("GRPC_DNS_RESOLVER", "native");
        }

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
        // This switch must be set before creating the GrpcChannel/HttpClient.
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        // The port number(5000) must match the port of the gRPC server.
        var channel = GrpcChannel.ForAddress("http://localhost:9080", new GrpcChannelOptions
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
