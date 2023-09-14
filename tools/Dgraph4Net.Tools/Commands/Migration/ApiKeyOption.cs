using System.CommandLine;

namespace Dgraph4Net.Tools.Commands.Migration;

internal sealed class ApiKeyOption : Option<string?>
{
    public ApiKeyOption() : base(new[] { "--api-key", "-apk" }, "The api key")
    {
    }
}
