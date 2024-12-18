using System.CommandLine;

namespace Dgraph4Net.Tools.Commands.Migration;

internal sealed class OutputOption()
    : Option<string>(["--output", "-o"], Application.ResolveOutputDirectory, "The output directory");
