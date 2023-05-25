using System.CommandLine;
using Grpc.Core;
using System.CommandLine.Parsing;

namespace Dgraph4Net.Tools.Commands.Migration;

internal sealed class ServerOption : Option<string>
{
    public ServerOption() : base("server", "The server address")
    {
        AddValidator(Validate);
    }

    private void Validate(OptionResult symbolResult)
    {
        // validate server address and if port is reachable for grpc protocol
        if (symbolResult == null)
        {
            throw new ArgumentNullException(nameof(symbolResult));
        }

        var value = symbolResult.GetValueOrDefault<string>();

        if (string.IsNullOrWhiteSpace(value))
        {
            symbolResult.ErrorMessage = "Server address is required";
        }
        else
        {
            var parts = value.Split(':');
            int port = 0;
            if (parts.Length != 2)
            {
                symbolResult.ErrorMessage = "Server address must be in the format <host>:<port>";
            }
            else if (!int.TryParse(parts[1], out port))
            {
                symbolResult.ErrorMessage = "Server port must be a number";
            }
            else if (port < 1 || port > 65535)
            {
                symbolResult.ErrorMessage = "Server port must be between 1 and 65535";
            }

            if (symbolResult.ErrorMessage != null)
            {
                return;
            }

            var host = parts[0];
            var channel = new Channel(host, port, ChannelCredentials.Insecure);

            try
            {
                channel.ConnectAsync().Wait();
            }
            catch (Exception ex)
            {
                symbolResult.ErrorMessage = $"Server address is not reachable: {ex.Message}";
            }
        }
    }
}
