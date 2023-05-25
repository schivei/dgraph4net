using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Logging;

namespace Dgraph4Net.Tools.Commands.Database;

internal sealed class DatabaseDropCommand : Command
{
    private ILogger _logger;
    public DatabaseDropCommand(ILogger<DatabaseDropCommand> logger,
               ProjectOption project, ServerArgument server, UserIdOption userId, PasswordOption password)
        : base("drop", "Drop the database")
    {
        _logger = logger;
        AddAlias("down");
        AddOption(project);
        AddArgument(server);
        AddOption(userId);
        AddOption(password);
        AddValidator(Validate);
        this.SetHandler(Exec, project, server, userId, password);
    }
    private async Task Exec(string project, string server, string? userId, string? password)
    {
        throw new NotImplementedException();
    }
    // check if password is not null if userId is provided
    private void Validate(CommandResult symbolResult)
    {
        var userId = symbolResult.GetValueForOption(Options.OfType<UserIdOption>().Single());
        var password = symbolResult.GetValueForOption(Options.OfType<PasswordOption>().Single());
        if (!string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(password))
            symbolResult.ErrorMessage = "Password is required if user id is provided";
    }
}
