using System.Collections.Immutable;
using System.CommandLine;
using System.Reflection;
using System.Runtime.InteropServices;
using Dgraph4Net.ActiveRecords;
using Microsoft.Extensions.Logging;

namespace Dgraph4Net.Tools.Commands.Migration;

internal class MigrationAddCommand : Command
{
    private readonly ILogger _logger;

    public MigrationAddCommand(ILogger<MigrationAddCommand> logger, ProjectOption projectLocation,
        OutputOption outputDirectory, MigrationNameArgument migrationName) : base("add", "Create a new migration")
    {
        _logger = logger;

        AddOption(projectLocation);
        AddOption(outputDirectory);
        AddArgument(migrationName);

        this.SetHandler(Exec, migrationName, projectLocation, outputDirectory);
    }

    private async Task Exec(string name, string projectLocation, string outputDirectory)
    {
        try
        {
            _logger.LogInformation("Add migration {name}", name);

            var fifo = new FileInfo(projectLocation);

            var outputs = new DirectoryInfo(Path.Combine(fifo.Directory.FullName, outputDirectory));

            // check if already has a file starting with same name
            if (outputs.Exists)
            {
                var files = outputs.GetFiles($"{name}_*.cs");
                if (files.Any())
                {
                    throw new Exception("Migration already exists");
                }
            }
            else
            {
                outputs.Create();
            }

            var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");

            var fileName = $"{name}_{suffix}.cs";

            var assembly = Application.BuildProject(projectLocation, _logger);

            // get all dependencies and sub dependencies assemblies
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

            if (!outputs.Exists)
                outputs.Create();

            var migrationFile = new FileInfo(Path.Combine(outputs.FullName, fileName));
            var scriptFile = new FileInfo(Path.Combine(outputs.FullName, $"{fileName}.schema"));

            var migrations = ClassMapping.Migrations.OrderByDescending(x => x.GeneratedAt);

            var mappings = ClassMapping.ClassMappings.Values
                .Where(x => !x.Type.IsAssignableTo(typeof(DgnMigration)))
                .OrderBy(x => x.Type.FullName)
                .ToArray();

            var script = ClassMapping.CreateScript();

            if (!migrations.Any())
            {
                await CreateAsync(migrationFile, scriptFile, fifo.Name.Replace(".csproj", ""), outputDirectory, script, mappings);
            }
            else
            {
                // check if script is different from last migration and apply differences only
                var lastMigration = migrations.First();

                lastMigration.Load();

                var lastScript = await lastMigration.GetSchema();

                if (script != lastScript)
                {
                    var allSchemaPredicates = ClassMap.Predicates
                        .Where(x => !x.Key.DeclaringType.IsAssignableTo(typeof(DgnMigration)))
                        .GroupBy(x => x.Value.ToSchemaPredicate())
                        .ToArray();

                    // get predicates not in lastScript
                    var newPredicates = allSchemaPredicates
                        .Where(x => !lastScript.Contains(x.Key))
                        .SelectMany(x => x)
                        .ToArray();

                    // get predicates mappings
                    mappings = newPredicates
                        .Select(x => ClassMapping.ClassMappings[x.Key.DeclaringType])
                        .GroupBy(x => x.Type.FullName)
                        .Select(x => x.First())
                        .ToArray();

                    var lastPredicates = lastScript.Trim().Split("type")[0]
                        .Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(x => x.Split(':')[0]);

                    var currentPredicates = script.Trim().Split("type")[0]
                        .Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(x => x.Split(':')[0]);

                    // get lastPredicates not in currentPredicates
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

                    await CreateAsync(migrationFile, scriptFile, fifo.Name.Replace(".csproj", ""), outputDirectory, script, Array.Empty<IClassMap>());
                }
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

    private async Task CreateAsync(FileInfo migrationFile, FileInfo scriptFile, string projectName, string output, string script, IClassMap[] mappings, ImmutableHashSet<string>? predicatesToRemove = null, ImmutableHashSet<string>? typesToRemove = null)
    {
        _logger.LogInformation("Create migration file {file} into {output}", migrationFile.Name, output);

        predicatesToRemove ??= ImmutableHashSet<string>.Empty;

        await using var swt = scriptFile.CreateText();
        await swt.WriteAsync(script);

        var className = migrationFile.Name.Replace(".cs", string.Empty);

        await using var mwt = migrationFile.CreateText();

        await mwt.WriteLineAsync("using Dgraph4Net.ActiveRecords;");

        // write usings of entities
        foreach (var @namespace in mappings.GroupBy(x => x.Type.Namespace).Select(x => x.Key))
        {
            await mwt.WriteLineAsync($"using {@namespace};");
        }

        await mwt.WriteLineAsync();

        var ns = string.Join(".", Path.Combine(projectName, output).Split(Path.DirectorySeparatorChar) ?? Array.Empty<string>());

        // normalize ns name to C# namespace conventions
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

        // write sets
        foreach (var entity in mappings)
        {
            await mwt.WriteLineAsync($"        SetType<{entity.Type.Name}>();");
        }

        // DropPredicates
        foreach (var predicate in predicatesToRemove)
        {
            await mwt.WriteLineAsync($"        DropPredicate(\"{predicate}\");");
        }

        // DropTypes
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

        // write drops on DgraphType of entities
        foreach (var entity in mappings)
        {
            await mwt.WriteLineAsync($"        DropType(\"{entity.DgraphType}\");");
        }

        await mwt.WriteLineAsync("    }");
        await mwt.WriteLineAsync("}");
        await mwt.WriteLineAsync();
    }
}
