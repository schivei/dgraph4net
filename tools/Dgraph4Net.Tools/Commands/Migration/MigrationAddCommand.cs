using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Reflection;

using Dgraph4Net.ActiveRecords;

using Microsoft.Extensions.Logging;

using ICM = Dgraph4Net.ActiveRecords.InternalClassMapping;

namespace Dgraph4Net.Tools.Commands.Migration;

/// <summary>
/// Represents a command to add a new migration.
/// </summary>
internal sealed class MigrationAddCommand : Command
{
    private readonly ILogger _logger;
    private readonly MigrationUpdateCommand _updateCommand;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationAddCommand"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="projectLocation">The project location option.</param>
    /// <param name="outputDirectory">The output directory option.</param>
    /// <param name="migrationName">The migration name argument.</param>
    /// <param name="serverOption">The server option.</param>
    /// <param name="userIdOption">The user ID option.</param>
    /// <param name="passwordOption">The password option.</param>
    /// <param name="updateOption">The update option.</param>
    /// <param name="apiKeyOption">The API key option.</param>
    /// <param name="updateCommand">The update command instance.</param>
    public MigrationAddCommand(ILogger<MigrationAddCommand> logger, ProjectOption projectLocation,
        OutputOption outputDirectory, MigrationNameArgument migrationName, ServerOption serverOption,
        UserIdOption userIdOption, PasswordOption passwordOption, UpdateOption updateOption,
        ApiKeyOption apiKeyOption, MigrationUpdateCommand updateCommand)
        : base("add", "Create a new migration")
    {
        _logger = logger;

        projectLocation.IsRequired = true;
        AddOption(projectLocation);
        outputDirectory.IsRequired = true;
        AddOption(outputDirectory);
        AddArgument(migrationName);
        AddOption(serverOption);
        AddOption(userIdOption);
        AddOption(passwordOption);
        AddOption(updateOption);
        AddOption(apiKeyOption);
        AddValidator(Validate);

        updateCommand.Options.OfType<ServerOption>().First().IsRequired = false;

        _updateCommand = updateCommand;

        this.SetHandler(Exec, migrationName, projectLocation, outputDirectory, updateOption, serverOption, userIdOption, passwordOption);
    }

    /// <summary>
    /// Executes the migration add command.
    /// </summary>
    /// <param name="name">The name of the migration.</param>
    /// <param name="projectLocation">The project location.</param>
    /// <param name="outputDirectory">The output directory.</param>
    /// <param name="update">Indicates whether to update the schema.</param>
    /// <param name="serverOption">The server option.</param>
    /// <param name="userIdOption">The user ID option.</param>
    /// <param name="passwordOption">The password option.</param>
    private async Task Exec(string name, string projectLocation, string outputDirectory, bool update, Dgraph4NetClient serverOption, string userIdOption, string passwordOption)
    {
        try
        {
            _logger.LogInformation("Add migration {name}", name);

            var fifo = new FileInfo(projectLocation);

            var outputs = new DirectoryInfo(Path.Combine(fifo.Directory.FullName, outputDirectory));

            if (outputs.Exists)
            {
                var files = outputs.GetFiles($"{name}_*.cs");
                if (files.Length != 0)
                {
                    throw new("Migration already exists");
                }
            }
            else
            {
                outputs.Create();
            }

            var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");

            var fileName = $"{name}_{suffix}.cs";

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

            if (ICM.ClassMappings.IsEmpty)
            {
                _logger.LogWarning("No mapping class found");
                return;
            }

            if (!outputs.Exists)
                outputs.Create();

            var migrationFile = new FileInfo(Path.Combine(outputs.FullName, fileName));
            var scriptFile = new FileInfo(Path.Combine(outputs.FullName, $"{fileName}.schema"));

            var migrations = ICM.Migrations.OrderByDescending(x => x.GeneratedAt);

            var mappings = ICM.ClassMappings.Values
                .Where(x => !x.Type.IsAssignableTo(typeof(DgnMigration)))
                .OrderBy(x => x.Type.FullName)
                .ToArray();

            var script = ICM.CreateScript();

            if (!migrations.Any())
            {
                await CreateAsync(migrationFile, scriptFile, fifo.Name.Replace(".csproj", ""), outputDirectory, script, mappings);
            }
            else
            {
                var lastMigration = migrations.First();

                lastMigration.Load();

                var lastScript = await lastMigration.GetSchema();

                if (script != lastScript)
                {
                    var allSchemaPredicates = ClassMap.Predicates
                        .Where(x => !x.Key.DeclaringType.IsAssignableTo(typeof(DgnMigration)))
                        .GroupBy(x => x.Value.ToSchemaPredicate())
                        .ToArray();

                    var newPredicates = allSchemaPredicates
                        .Where(x => !lastScript.Contains(x.Key))
                        .SelectMany(x => x)
                        .ToArray();

                    mappings = newPredicates
                        .Select(x => ICM.ClassMappings[x.Key.DeclaringType])
                        .GroupBy(x => x.Type.FullName)
                        .Select(x => x.First())
                        .ToArray();

                    var lastPredicates = lastScript.Trim().Split("type")[0]
                        .Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(x => x.Split(':')[0]);

                    var currentPredicates = script.Trim().Split("type")[0]
                        .Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(x => x.Split(':')[0]);

                    var removedPredicates = lastPredicates
                        .Where(x => !currentPredicates.Contains(x))
                        .ToImmutableHashSet();

                    var lastTypes = lastScript.Trim().Split("\n").Where(x => x.StartsWith("type"))
                        .Select(x => x.Split(' ')[1].Trim())
                        .ToArray();

                    var currentTypes = script.Trim().Split("\n").Where(x => x.StartsWith("type"))
                        .Select(x => x.Split(' ')[1].Trim())
                        .ToArray();

                    var removedTypes = lastTypes
                        .Where(x => !currentTypes.Contains(x))
                        .ToImmutableHashSet();

                    await CreateAsync(migrationFile, scriptFile, fifo.Name.Replace(".csproj", ""), outputDirectory, script, mappings, removedPredicates, removedTypes);
                }
                else
                {
                    _logger.LogInformation("No changes found");

                    await CreateAsync(migrationFile, scriptFile, fifo.Name.Replace(".csproj", ""), outputDirectory, script, []);
                }
            }

            if (update)
            {
                await _updateCommand.Exec(projectLocation, serverOption);
            }

            _logger.LogInformation("Migration {name} added", name);
            Console.WriteLine("To remove migration use:");
            Console.WriteLine($"  dgn migration remove {name}");
        }
        catch (Exception ex)
        {
            _logger.LogError("{Message}", ex.Message);
        }
    }

    /// <summary>
    /// Creates the migration files.
    /// </summary>
    /// <param name="migrationFile">The migration file.</param>
    /// <param name="scriptFile">The script file.</param>
    /// <param name="projectName">The project name.</param>
    /// <param name="output">The output directory.</param>
    /// <param name="script">The migration script.</param>
    /// <param name="mappings">The class mappings.</param>
    /// <param name="predicatesToRemove">The predicates to remove.</param>
    /// <param name="typesToRemove">The types to remove.</param>
    private async Task CreateAsync(FileInfo migrationFile, FileInfo scriptFile, string projectName, string output, string script, IClassMap[] mappings, ImmutableHashSet<string>? predicatesToRemove = null, ImmutableHashSet<string>? typesToRemove = null)
    {
        mappings ??= [];
        predicatesToRemove ??= [];
        typesToRemove ??= [];

        _logger.LogInformation("Create migration file {file} into {output}", migrationFile.Name, output);

        predicatesToRemove ??= [];

        await using var swt = scriptFile.CreateText();
        await swt.WriteAsync(script);

        var className = migrationFile.Name.Replace(".cs", string.Empty);

        await using var mwt = migrationFile.CreateText();

        await mwt.WriteLineAsync("using Dgraph4Net.ActiveRecords;");

        foreach (var @namespace in mappings.GroupBy(x => x.Type.Namespace).Select(x => x.Key))
        {
            await mwt.WriteLineAsync($"using {@namespace};");
        }

        await mwt.WriteLineAsync();

        var ns = string.Join(".", Path.Combine(projectName, output).Split(Path.DirectorySeparatorChar) ?? []);

        ns = ns?.Replace(" ", ".").Replace("-", ".");

        await mwt.WriteLineAsync($"namespace {ns};");

        await mwt.WriteLineAsync();

        await mwt.WriteLineAsync($"internal sealed class {className} : Migration");
        await mwt.WriteLineAsync("{");
        await mwt.WriteLineAsync("    ///<summary>");
        await mwt.WriteLineAsync("    /// Apply changes to database");
        await mwt.WriteLineAsync("    ///</summary>");
        await mwt.WriteLineAsync("    protected override void Up()");
        await mwt.WriteLineAsync("    {");

        foreach (var entity in mappings)
        {
            await mwt.WriteLineAsync($"        SetType<{entity.Type.Name}>();");
        }

        foreach (var predicate in predicatesToRemove)
        {
            await mwt.WriteLineAsync($"        DropPredicate(\"{predicate}\");");
        }

        foreach (var type in typesToRemove)
        {
            await mwt.WriteLineAsync($"        DropType(\"{type}\");");
        }

        await mwt.WriteLineAsync("    }");
        await mwt.WriteLineAsync();
        await mwt.WriteLineAsync("    ///<summary>");
        await mwt.WriteLineAsync("    /// Revert changes made by Up method");
        await mwt.WriteLineAsync("    ///</summary>");
        await mwt.WriteLineAsync("    ///<remarks>");
        await mwt.WriteLineAsync("    /// It is not necessary to recreate removed predicates and types because the migration is applied in order");
        await mwt.WriteLineAsync("    ///</remarks>");
        await mwt.WriteLineAsync("    protected override void Down()");
        await mwt.WriteLineAsync("    {");

        foreach (var entity in mappings)
        {
            await mwt.WriteLineAsync($"        DropType(\"{entity.DgraphType}\");");
        }

        await mwt.WriteLineAsync("    }");
        await mwt.WriteLineAsync("}");
        await mwt.WriteLineAsync();
    }

    /// <summary>
    /// Validates the command options.
    /// </summary>
    /// <param name="symbolResult">The command result.</param>
    private void Validate(CommandResult symbolResult)
    {
        var update = symbolResult.GetValueForOption(Options.OfType<UpdateOption>().Single());
        if (!update)
            return;

        Options.OfType<ServerOption>().Single().IsRequired = true;

        var server = symbolResult.GetValueForOption(Options.OfType<ServerOption>().Single());
        if (server is null)
        {
            symbolResult.ErrorMessage = "Server is required";
        }
    }
}
