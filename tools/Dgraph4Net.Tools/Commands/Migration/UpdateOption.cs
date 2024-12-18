using System.CommandLine;

namespace Dgraph4Net.Tools.Commands.Migration;

internal sealed class UpdateOption() : Option<bool>(["--update", "-u"], "Update the database");
