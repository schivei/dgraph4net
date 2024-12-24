using System.CommandLine;

namespace Dgraph4Net.Tools.Commands.Migration;

/// <summary>
/// Represents an option for specifying a password in the command line.
/// </summary>
internal sealed class PasswordOption() : Option<string?>(aliases: ["--password", "-pwd"], description: "The password");
