using System.CommandLine;
using Dgraph4Net.Tools.Commands;

namespace Dgraph4Net.Tools;

/// <summary>
/// Represents the root command for the Dgraph4Net migration tool.
/// </summary>
internal sealed class ApplicationCommand : RootCommand
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationCommand"/> class.
    /// </summary>
    /// <param name="migrationCommand">The migration command to be added to the root command.</param>
    public ApplicationCommand(MigrationCommand migrationCommand) : base("The Dgraph4Net migration tool")
    {
        AddCommand(migrationCommand);
    }
}
