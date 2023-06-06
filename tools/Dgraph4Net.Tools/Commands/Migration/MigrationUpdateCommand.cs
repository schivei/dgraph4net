using System.Collections.Immutable;
using System.CommandLine;
using System.Reflection;
using Dgraph4Net.ActiveRecords;
using Microsoft.Extensions.Logging;

namespace Dgraph4Net.Tools.Commands.Migration;

internal sealed class MigrationUpdateCommand : Command
{
    private readonly ILogger _logger;

    public MigrationUpdateCommand(ILogger<MigrationUpdateCommand> logger, ProjectOption projectLocation,
        ServerOption serverOption, UserIdOption userIdOption, PasswordOption passwordOption) : base("update", "Update database schema")
    {
        _logger = logger;

        projectLocation.IsRequired = true;
        AddOption(projectLocation);
        serverOption.IsRequired = true;
        AddOption(serverOption);
        AddOption(userIdOption);
        AddOption(passwordOption);
        AddAlias("up");

        this.SetHandler(Exec, projectLocation, serverOption);
    }

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
                typeof(InternalClassMapping).Assembly
            };

            InternalClassMapping.SetDefaults(mergedAssemblies.ToArray());

            InternalClassMapping.Map(mergedAssemblies.ToArray());

            if (!InternalClassMapping.ClassMappings.Any())
            {
                _logger.LogWarning("No mapping class found");
                return;
            }

            var migrations = InternalClassMapping.Migrations;

            await InternalClassMapping.EnsureAsync(client);

            await using var txn = client.NewTransaction(false, false);

            var dgnType = InternalClassMapping.GetDgraphType(typeof(DgnMigration));

            _logger.LogInformation("Get last migration");
            var migs = await txn.Query<DgnMigration>("dgn", @$"{{
  dgn(func: type({dgnType}), orderdesc: dgn.generated_at, first: 1) {{
    uid
    dgraph.type
    dgn.name
    dgn.generated_at
    dgn.applied_at
  }}
}}");

            var lastMigration = migs.SingleOrDefault();

            // get migrations after last migration
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
