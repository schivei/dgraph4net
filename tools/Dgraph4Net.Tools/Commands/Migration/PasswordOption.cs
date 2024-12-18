using System.CommandLine;

namespace Dgraph4Net.Tools.Commands.Migration;

internal sealed class PasswordOption() : Option<string?>(["--password", "-pwd"], "The password");
