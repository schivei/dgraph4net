using System.CommandLine;

namespace Dgraph4Net.Tools.Commands;

internal class ProjectOption : Option<string>
{
    public ProjectOption() : base(new[] { "--project", "-p" }, Application.ResolveProjectLocation, "The project location")
    { }
}
