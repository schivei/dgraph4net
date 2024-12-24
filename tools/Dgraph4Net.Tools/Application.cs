using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Dgraph4Net.Tools;

/// <summary>
/// Represents the main application class for the Dgraph4Net migration tool.
/// </summary>
internal sealed class Application(ILogger<Application> logger, ApplicationCommand appCommand)
{
    private readonly ILogger _logger = logger;

    /// <summary>
    /// Executes the application with the specified arguments.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    public async Task ExecuteAsync(string[] args)
    {
        try
        {
            await appCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            _logger.LogError("{Message}", ex.Message);
        }
    }

    /// <summary>
    /// Resolves the output directory for migrations.
    /// </summary>
    /// <returns>The output directory path.</returns>
    internal static string ResolveOutputDirectory() => "Migrations";

    /// <summary>
    /// Resolves the project location by finding the .csproj file in the current directory.
    /// </summary>
    /// <returns>The path to the .csproj file.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no project or multiple projects are found.</exception>
    internal static string ResolveProjectLocation()
    {
        var projectLocation = Directory.GetCurrentDirectory();
        var projectFiles = Directory.GetFiles(projectLocation, "*.csproj");

        return projectFiles.Length switch
        {
            0 => throw new InvalidOperationException("No project found"),
            > 1 => throw new InvalidOperationException("Multiple projects found"),
            _ => projectFiles[0]
        };
    }

    /// <summary>
    /// Builds the project located at the specified path.
    /// </summary>
    /// <param name="projectLocation">The path to the .csproj file.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>The built assembly.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the build process fails or the assembly is not found.</exception>
    internal static Assembly BuildProject(string projectLocation, ILogger logger)
    {
        var fullPath = Path.GetFullPath(projectLocation);

        logger.LogInformation("Build project {projectLocation}", fullPath);

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{fullPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Process not found");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(output + "\n" + error);
        }

        var csprojName = Path.GetFileNameWithoutExtension(projectLocation);
        var outputPath = Array.Find(output.Split('\n'), x => x.Contains(csprojName + " ->"))?.Split("->")[1].Trim();

        var assembly = Assembly.LoadFrom(outputPath);
        return assembly ?? throw new InvalidOperationException("Assembly not found");
    }
}
