using System.CommandLine;
using Dgraph4Net.ActiveRecords;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Grpc.Core;
using System.CommandLine.Parsing;
using System.Data;
using System.Threading;

namespace Dgraph4Net.Tools.Commands.Migration;

internal class MigrationRemoveCommand : Command
{
    private readonly ILogger _logger;

    public MigrationRemoveCommand(ILogger<MigrationRemoveCommand> logger, ProjectOption projectLocation,
        OutputOption outputDirectory, MigrationNameArgument migrationName, ServerOption serverOption,
        UserIdOption userIdOption, PasswordOption passwordOption) : base("remove", "Remove a migration")
    {
        _logger = logger;

        AddOption(projectLocation);
        AddOption(outputDirectory);
        AddArgument(migrationName);
        AddOption(serverOption);
        AddOption(userIdOption);
        AddOption(passwordOption);

        this.SetHandler(Exec, migrationName, projectLocation, outputDirectory, serverOption, userIdOption, passwordOption);
    }

    private async Task Exec(string name, string projectLocation, string outputDirectory, string serverOption, string userIdOption, string passwordOption)
    {
        _logger.LogInformation("Remove migration {name}", name);

        var fifo = new FileInfo(projectLocation);

        var outputs = new DirectoryInfo(Path.Combine(fifo.Directory.FullName, outputDirectory));

        // check if migration exists
        if (!outputs.Exists)
            throw new Exception("Migration not found");

        var files = outputs.GetFiles($"{name}_*.cs");
        if (!files.Any())
            throw new Exception("Migration not found");

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
                typeof(ClassMapping).Assembly
            };

        ClassMapping.Map(mergedAssemblies.ToArray());

        if (!ClassMapping.ClassMappings.Any())
        {
            _logger.LogWarning("No mapping class found");
            return;
        }

        var migration = ClassMapping.Migrations.FirstOrDefault(x => x.Name == name) ?? throw new Exception("Migration not found");

        // check if have more migrations after this
        var migrations = ClassMapping.Migrations.Where(x => x.GeneratedAt > migration.GeneratedAt).ToList();

        migrations.Add(migration);

        var host = serverOption.Split(":")[0];
        var port = int.Parse(serverOption.Split(":")[1]);

        var client = new Dgraph4NetClient(new Channel(host, port, ChannelCredentials.Insecure));

        if (!string.IsNullOrEmpty(userIdOption) && !string.IsNullOrEmpty(passwordOption))
            await client.LoginAsync(userIdOption, passwordOption);

        // get previous migration

        await using var txn = client.NewTransaction(false, false);

        var dgnType = ClassMapping.GetDgraphType(typeof(DgnMigration));

        var migs = await txn.QueryWithVars<DgnMigration>("dgn", @$"query Q($date: string) {{
  dgn(func: type({dgnType}), orderdesc: generated_at, first: 1) @filter(lt(generated_at, $date)) {{
    uid
    dgraph.type
    name
    generated_at
    applied_at
  }}
}}", new(){ ["date"] = migration.GeneratedAt.ToString("O") });

        // get previous ClassMapping.Migrations from migs[0]
        var previousMigration = migs.Any() ? ClassMapping.Migrations.FirstOrDefault(x => x.Name == migs[0].Name) : null;
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

    // check if password is not null if userId is provided
    private void Validate(CommandResult symbolResult)
    {
        var userId = symbolResult.GetValueForOption(Options.OfType<UserIdOption>().Single());
        var password = symbolResult.GetValueForOption(Options.OfType<PasswordOption>().Single());

        if (!string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(password))
            symbolResult.ErrorMessage = "Password is required if user id is provided";
    }
}
