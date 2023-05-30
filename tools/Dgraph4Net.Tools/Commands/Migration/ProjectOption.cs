using System.CommandLine;

namespace Dgraph4Net.Tools.Commands;

internal sealed class ProjectOption : Option<string>
{
    public ProjectOption() : base(new[] { "--project", "-p" }, Application.ResolveProjectLocation, "The project location")
    { }
}
