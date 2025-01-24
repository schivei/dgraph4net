using System.CommandLine;

namespace Dgraph4Net.Tools.Commands.Migration;

/// <summary>
/// Represents an option to update the database.
/// </summary>
internal sealed class UpdateOption() : Option<bool>(["--update", "-u"], "Update the database");
