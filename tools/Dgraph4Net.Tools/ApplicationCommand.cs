using System.CommandLine;
using Dgraph4Net.Tools.Commands;

namespace Dgraph4Net.Tools;

internal sealed class ApplicationCommand : RootCommand
{
    public ApplicationCommand(MigrationCommand migrationCommand, DatabaseCommand databaseCommand) : base("The Dgraph4Net migration tool")
    {
        AddCommand(migrationCommand);
        AddCommand(databaseCommand);
    }
}
