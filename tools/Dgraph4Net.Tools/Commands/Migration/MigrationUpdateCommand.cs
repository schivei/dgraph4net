using System.Collections.Immutable;
using System.CommandLine;
using System.Reflection;

using Dgraph4Net.ActiveRecords;

using Microsoft.Extensions.Logging;

using ICM = Dgraph4Net.ActiveRecords.InternalClassMapping;

namespace Dgraph4Net.Tools.Commands.Migration;

/// <summary>
/// Command to update the database schema.
/// </summary>
internal sealed class MigrationUpdateCommand : Command
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationUpdateCommand"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="projectLocation">The project location option.</param>
    /// <param name="serverOption">The server option.</param>
    /// <param name="userIdOption">The user ID option.</param>
    /// <param name="passwordOption">The password option.</param>
    /// <param name="apiKeyOption">The API key option.</param>
    public MigrationUpdateCommand(ILogger<MigrationUpdateCommand> logger, ProjectOption projectLocation,
        ServerOption serverOption, UserIdOption userIdOption, PasswordOption passwordOption,
        ApiKeyOption apiKeyOption) : base("update", "Update database schema")
    {
        _logger = logger;

        projectLocation.IsRequired = true;
        AddOption(projectLocation);
        serverOption.IsRequired = true;
        AddOption(serverOption);
        AddOption(userIdOption);
        AddOption(passwordOption);
        AddOption(apiKeyOption);
        AddAlias("up");

        this.SetHandler(Exec, projectLocation, serverOption);
    }

    /// <summary>
    /// Executes the update command.
    /// </summary>
    /// <param name="projectLocation">The project location.</param>
    /// <param name="client">The Dgraph client.</param>
    internal async Task Exec(string projectLocation, Dgraph4NetClient client)
    {
        try
        {
            _logger.LogInformation("Update database schema");

            var assembly = Application.BuildProject(projectLocation, _logger);

            var assemblies = assembly.GetReferencedAssemblies().Select(a =>
            {
                try
                {
                    return (Assembly?)Assembly.Load(a);
                }
                catch
                {
                    return null;
                }
            }).Where(a => a != null).ToImmutableArray();

            var mergedAssemblies = new HashSet<Assembly>(assemblies)
            {
                assembly,
                typeof(ICM).Assembly
            };

            ICM.SetDefaults([.. mergedAssemblies]);

            ICM.Map([.. mergedAssemblies]);

            if (ICM.ClassMappings.IsEmpty)
            {
                _logger.LogWarning("No mapping class found");
                return;
            }

            var migrations = ICM.Migrations;

            await ICM.EnsureAsync(client);

            await using var txn = client.NewTransaction(false, false);

            var dgnType = ICM.GetDgraphType(typeof(DgnMigration));

            _logger.LogInformation("Get last migration");
            var migs = await txn.Query<DgnMigration>("dgn", $$"""
                                                              {
                                                                dgn(func: type({{dgnType}}), orderdesc: dgn.generated_at, first: 1) {
                                                                  uid
                                                                  dgraph.type
                                                                  dgn.name
                                                                  dgn.generated_at
                                                                  dgn.applied_at
                                                                }
                                                              }
                                                              """);

            var lastMigration = migs.SingleOrDefault();

            var newMigrations = (lastMigration is null
                ? migrations :
                migrations.Where(x => x.GeneratedAt > lastMigration.GeneratedAt))
                .OrderBy(x => x.GeneratedAt).ToImmutableArray();

            if (!newMigrations.Any())
            {
                _logger.LogInformation("0 of {Total} migrations pending.", migrations.Count);
                return;
            }

            _logger.LogInformation("Apply pending migrations");
            foreach (var mig in newMigrations)
            {
                _logger.LogInformation("Appling migration {Name}", mig.Name);
                mig.SetClient(client);
                await mig.MigrateUp();
                _logger.LogInformation("Migration {Name} applied", mig.Name);
            }

            _logger.LogInformation("All migrations applied");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "{Message}", ex.Message);
        }
    }
}
