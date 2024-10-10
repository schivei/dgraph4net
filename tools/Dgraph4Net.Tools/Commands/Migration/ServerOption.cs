using System.CommandLine;
using Grpc.Core;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Logging;

namespace Dgraph4Net.Tools.Commands.Migration;

internal sealed class ServerOption(ILogger<ServerOption> logger) : Option<Dgraph4NetClient>(["--server", "-s"], Parse(logger), false, "The server address")
{
    private static Dgraph4NetClient s_client;
    private static readonly object s_lock = new();

    private static ParseArgument<Dgraph4NetClient> Parse(ILogger logger)
    {
        return (result) =>
        {
            if (s_client is not null)
                return s_client;

            lock (s_lock)
            {
                var address = result.Tokens[0].Value.Trim();

                logger.LogInformation("Testing connection to server {server}", address);

                var userId = result.Parent.Parent.GetValueForOption((result.Parent.Parent.Symbol as Command).Options.OfType<UserIdOption>().First());
                var password = result.Parent.Parent.GetValueForOption((result.Parent.Parent.Symbol as Command).Options.OfType<PasswordOption>().First());
                var apk = result.Parent.Parent.GetValueForOption((result.Parent.Parent.Symbol as Command).Options.OfType<ApiKeyOption>().First());

                var channel = new Channel(address, ChannelCredentials.Insecure);

                if (apk is not null)
                {
                    logger.LogInformation("Authenticating in server {server}", address);

                    channel = new Channel(address, ChannelCredentials.Create(ChannelCredentials.SecureSsl, CallCredentials.FromInterceptor((_, metadata) =>
                    {
                        metadata.Add("authorization", apk);

                        return Task.CompletedTask;
                    })));
                }

                var client = new Dgraph4NetClient(channel);

                if (userId is not null && password is not null)
                {
                    logger.LogInformation("Authenticating in server {server}", address);

                    client.Login(userId, password);
                }

                using var txn = client.NewTransaction();

                txn.Query("schema{}").GetAwaiter().GetResult();

                logger.LogInformation("Connection to server {server} established", address);

                s_client = client;
            }

            return s_client;
        };
    }
}
