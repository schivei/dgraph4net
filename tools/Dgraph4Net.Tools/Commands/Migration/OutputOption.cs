using System.CommandLine;

namespace Dgraph4Net.Tools.Commands.Migration;

/// <summary>
/// Represents the output option for the migration command.
/// </summary>
internal sealed class OutputOption()
    : Option<string>(["--output", "-o"], Application.ResolveOutputDirectory, "The output directory");
