using System.CommandLine;

namespace Dgraph4Net.Tools.Commands.Migration;

internal sealed class ApiKeyOption() : Option<string?>(["--api-key", "-apk"], "The api key");
