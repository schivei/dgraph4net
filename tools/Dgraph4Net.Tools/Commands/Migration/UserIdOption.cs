using System.CommandLine;

// ReSharper disable once CheckNamespace
namespace Dgraph4Net.Tools.Commands;

internal sealed class UserIdOption() : Option<string?>(["--user", "-uid"], "The user id");
