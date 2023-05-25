using System.CommandLine;
using Dgraph4Net.Tools.Commands.Database;

namespace Dgraph4Net.Tools.Commands;

internal class DatabaseCommand : Command
{
    public DatabaseCommand(DatabaseUpdateCommand updateCommand, DatabaseDropCommand dropCommand)
        : base("database", "Update or drop the database")
    {
        AddCommand(updateCommand);
        AddCommand(dropCommand);
    }
}
