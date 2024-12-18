using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Dgraph4Net.Tools;

internal sealed class Application(ILogger<Application> logger, ApplicationCommand appCommand)
{
    private readonly ILogger _logger = logger;

    /// <summary>
    /// Execute the command
    /// </summary>
    /// <param name="args"></param>
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
    /// Get output directory if not provided
    /// </summary>
    /// <returns>The output directory</returns>
    /// <exception cref="InvalidOperationException"/>
    internal static string ResolveOutputDirectory() => "Migrations";

    /// <summary>
    /// Get current project location if not provided
    /// </summary>
    /// <returns>.csproj location</returns>
    /// <exception cref="InvalidOperationException"/>
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
    /// Build a project at runtime and return the assembly
    /// </summary>
    /// <param name="projectLocation"></param>
    /// <param name="logger"></param>
    /// <returns><see cref="Assembly"/></returns>
    /// <exception cref="InvalidOperationException"></exception>
    internal static Assembly BuildProject(string projectLocation, ILogger logger)
    {
        var fullPath = Path.GetFullPath(projectLocation);

        logger.LogInformation("Build project {projectLocation}", fullPath);

        // compile using dotnet cli and gets std outputs and throw errors
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

        // get output path from std output
        var outputPath = Array.Find(output.Split('\n'), x => x.Contains(csprojName + " ->"))?.Split("->")[1].Trim();

        // get assembly from result
        var assembly = Assembly.LoadFrom(outputPath);
        return assembly ?? throw new InvalidOperationException("Assembly not found");
    }
}
