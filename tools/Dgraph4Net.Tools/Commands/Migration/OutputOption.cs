using System.CommandLine;

namespace Dgraph4Net.Tools.Commands.Migration;

internal class OutputOption : Option<string>
{
    public OutputOption() : base(new[] { "--output", "-o" }, Application.ResolveOutputDirectory, "The output directory")
    { }
}
