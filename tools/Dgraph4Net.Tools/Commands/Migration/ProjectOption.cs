using System.CommandLine;

// ReSharper disable once CheckNamespace
namespace Dgraph4Net.Tools.Commands;

internal sealed class ProjectOption()
    : Option<string>(["--project", "-p"], Application.ResolveProjectLocation, "The project location");
