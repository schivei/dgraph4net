using System.CommandLine;

namespace Dgraph4Net.Tools.Commands.Migration;

/// <summary>
/// Represents an option for specifying the project location.
/// </summary>
internal sealed class ProjectOption()
    : Option<string>(["--project", "-p"], Application.ResolveProjectLocation, "The project location")
{ }
