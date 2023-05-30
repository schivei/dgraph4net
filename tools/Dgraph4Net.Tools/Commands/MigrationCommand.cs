using System.CommandLine;
using Dgraph4Net.Tools.Commands.Migration;

namespace Dgraph4Net.Tools.Commands;

internal sealed class MigrationCommand : Command
{
    public MigrationCommand(MigrationAddCommand addCommand, MigrationRemoveCommand removeCommand, MigrationUpdateCommand updateCommand) : base("migration", "Create or remove a migrations")
    {
        AddCommand(addCommand);
        AddCommand(removeCommand);
        AddCommand(updateCommand);
    }
}
