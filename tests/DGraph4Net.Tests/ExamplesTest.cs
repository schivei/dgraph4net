using System;
using System.Threading.Tasks;
using DGraph4Net.Services;
using Grpc.Core;
using Grpc.Net.Client;
using Xunit;

namespace DGraph4Net.Tests
{
    [Collection("DGraph4Net")]
    public abstract class ExamplesTest
    {
        protected static DGraph GetDgraphClient()
        {
            // This switch must be set before creating the GrpcChannel/HttpClient.
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            // The port number(5000) must match the port of the gRPC server.
            var channel = GrpcChannel.ForAddress("http://dev-360-es01beta.eastus2.cloudapp.azure.com:9080", new GrpcChannelOptions {
                Credentials = ChannelCredentials.Insecure
            });

            var dg = new DGraph(channel);

            dg.Alter(new Operation { DropAll = true }).ConfigureAwait(false)
                .GetAwaiter().GetResult();

            return dg;
        }
    }
}
