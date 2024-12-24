using System.CommandLine;
using Dgraph4Net.Tools.Commands.Migration;

namespace Dgraph4Net.Tools.Commands;

/// <summary>
/// Represents the migration command which includes add, remove, and update subcommands.
/// </summary>
internal sealed class MigrationCommand : Command
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationCommand"/> class.
    /// </summary>
    /// <param name="addCommand">The command to add a new migration.</param>
    /// <param name="removeCommand">The command to remove an existing migration.</param>
    /// <param name="updateCommand">The command to update the database schema.</param>
    public MigrationCommand(MigrationAddCommand addCommand, MigrationRemoveCommand removeCommand, MigrationUpdateCommand updateCommand)
        : base("migration", "Create or remove a migrations")
    {
        AddCommand(addCommand);
        AddCommand(removeCommand);
        AddCommand(updateCommand);
    }
}
