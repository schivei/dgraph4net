using System.CommandLine;

namespace Dgraph4Net.Tools.Commands.Migration;

/// <summary>
/// Represents an option for specifying the API key in the command line interface.
/// </summary>
internal sealed class ApiKeyOption() : Option<string?>(["--api-key", "-apk"], "The api key");
