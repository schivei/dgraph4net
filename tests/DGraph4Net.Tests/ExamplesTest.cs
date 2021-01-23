using System;
using System.Threading;
using System.Threading.Tasks;

using Api;

using Grpc.Core;
using Grpc.Net.Client;

using Xunit;

namespace Dgraph4Net.Tests
{
    [Collection("Dgraph4Net")]
    public class ExamplesTest : Assert
    {
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

        protected static async Task CleanPredicates(params string[] preds)
        {
            var dg = GetDgraphClient();

            foreach (var attr in preds)
            {
                await dg.Alter(new Operation
                {
                    DropValue = attr,
                    DropOp = Operation.Types.DropOp.Attr
                });
            }
        }

        protected static async Task CleanTypes(params string[] types)
        {
            var dg = GetDgraphClient();

            foreach (var type in types)
            {
                await dg.Alter(new Operation
                {
                    DropValue = type,
                    DropOp = Operation.Types.DropOp.Type
                });
            }
        }
    }
}
