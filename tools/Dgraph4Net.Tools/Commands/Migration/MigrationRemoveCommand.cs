using System.CommandLine;
using System.Reflection;

using Dgraph4Net.ActiveRecords;

using Microsoft.Extensions.Logging;

using ICM = Dgraph4Net.ActiveRecords.InternalClassMapping;

namespace Dgraph4Net.Tools.Commands.Migration;

internal sealed class MigrationRemoveCommand : Command
{
    private readonly ILogger _logger;

    public MigrationRemoveCommand(ILogger<MigrationRemoveCommand> logger, ProjectOption projectLocation,
        OutputOption outputDirectory, MigrationNameArgument migrationName, ServerOption serverOption,
        UserIdOption userIdOption, PasswordOption passwordOption, ApiKeyOption apiKeyOption)
        : base("remove", "Remove a migration")
    {
        _logger = logger;

        projectLocation.IsRequired = true;
        AddOption(projectLocation);
        outputDirectory.IsRequired = true;
        AddOption(outputDirectory);
        AddArgument(migrationName);
        serverOption.IsRequired = true;
        AddOption(serverOption);
        AddOption(userIdOption);
        AddOption(passwordOption);
        AddOption(apiKeyOption);

        this.SetHandler(Exec, migrationName, projectLocation, outputDirectory, serverOption);
    }

    private async Task Exec(string name, string projectLocation, string outputDirectory, Dgraph4NetClient client)
    {
        _logger.LogInformation("Remove migration {name}", name);

        var fifo = new FileInfo(projectLocation);

        var outputs = new DirectoryInfo(Path.Combine(fifo.Directory.FullName, outputDirectory));

        // check if migration exists
        if (!outputs.Exists)
            throw new("Migration not found");

        var files = outputs.GetFiles($"{name}_*.cs");
        if (!files.Any())
            throw new("Migration not found");

        var file = files[0];

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
        }).Where(x => x is not null).OfType<Assembly>().ToList();

        var mergedAssemblies = new HashSet<Assembly>(assemblies)
            {
                assembly,
                typeof(ICM).Assembly
            };

        ICM.SetDefaults([.. mergedAssemblies]);

        ICM.Map([.. mergedAssemblies]);

        if (!ICM.ClassMappings.Any())
        {
            _logger.LogWarning("No mapping class found");
            return;
        }

        var migration = ICM.Migrations.FirstOrDefault(x => x.Name == name) ?? throw new("Migration not found");

        // check if have more migrations after this
        var migrations = ICM.Migrations.Where(x => x.GeneratedAt > migration.GeneratedAt).ToList();

        migrations.Add(migration);

        await ICM.EnsureAsync(client);

        // get previous migration

        await using var txn = client.NewTransaction(false, false);

        var dgnType = ICM.GetDgraphType(typeof(DgnMigration));

        var migs = await txn.QueryWithVars<DgnMigration>("dgn", $$"""
                                                                  query Q($date: string) {
                                                                    dgn(func: type({{dgnType}}), orderdesc: dgn.generated_at, first: 1) @filter(lt(dgn.generated_at, $date)) {
                                                                      uid
                                                                      dgraph.type
                                                                      dgn.name
                                                                      dgn.generated_at
                                                                      dgn.applied_at
                                                                    }
                                                                  }
                                                                  """, new(){ ["date"] = migration.GeneratedAt.ToString("O") });

        // get previous ClassMapping.Migrations from migs[0]
        var previousMigration = migs.Any() ? ICM.Migrations.FirstOrDefault(x => x.Name == migs[0].Name) : null;
        if (previousMigration is not null)
        {
            _logger.LogInformation("Set previous '{Migration}' migration as current", previousMigration.Name);
        }

        foreach (var mig in migrations.OrderByDescending(x => x.GeneratedAt))
        {
            _logger.LogInformation("Migrate down '{Migration}'", mig.Name);

            mig.SetClient(client);
            await mig.MigrateDown();
        }

        // remove migration files
        foreach (var mig in migrations)
        {
            _logger.LogInformation("Remove migration '{Migration}'", mig.Name);

            var fileToRemove = outputs.GetFiles($"{mig.Name}_*.*").First();
            fileToRemove.Delete();
        }

        if (previousMigration is not null)
        {
            _logger.LogInformation("Set previous '{Migration}' migration as current", previousMigration.Name);
            previousMigration.SetClient(client);
            await previousMigration.MigrateDown();
            await previousMigration.MigrateUp();
        }

        _logger.LogInformation("Migration '{Migration}' removed", migration.Name);
    }
}
