using System.CommandLine;

namespace Dgraph4Net.Tools.Commands.Migration;

/// <summary>
/// Represents an option for specifying the user ID.
/// </summary>
internal sealed class UserIdOption() : Option<string?>(["--user", "-uid"], "The user id");
