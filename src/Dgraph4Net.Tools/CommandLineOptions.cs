
using Dgraph4Net.Tools.Internal;
using Dgraph4Net.Tools.Internals;

using Microsoft.Extensions.CommandLineUtils;

namespace Dgraph4Net.Tools
{
    internal class CommandLineOptions
    {
        public ICommand Command { get; set; }
        public string Configuration { get; private set; }
        public bool IsHelp { get; private set; }
        public bool IsVerbose { get; private set; }
        public string Project { get; private set; }

        public static CommandLineOptions Parse(string[] args, IConsole console)
        {
            var app = new CommandLineApplication()
            {
                Out = console.Out,
                Error = console.Error,
                Name = "dgraph tools",
                FullName = "Dgraph Helper Tools",
                Description = "Manage database and mappings"
            };

            app.HelpOption();
            app.VersionOptionFromAssemblyAttributes(typeof(Program).Assembly);

            var optionVerbose = app.VerboseOption();

            var optionProject = app.Option("-p|--project <PROJECT>", "Path to project. Defaults to searching the current directory.",
                CommandOptionType.SingleValue, inherited: true);

            var optionConfig = app.Option("-c|--configuration <CONFIGURATION>", $"The project configuration to use. Defaults to 'Debug'.",
                CommandOptionType.SingleValue, inherited: true);

            var options = new CommandLineOptions();

            app.Command("migrate", c => MigrateCommand.Configure(c, options));

            // Show help information if no subcommand/option was specified.
            app.OnExecute(() => app.ShowHelp());

            if (app.Execute(args) != 0)
            {
                // when command line parsing error in subcommand
                return null;
            }

            options.Configuration = optionConfig.Value();
            options.IsHelp = app.IsShowingInformation;
            options.IsVerbose = optionVerbose.HasValue();
            options.Project = optionProject.Value();

            return options;
        }
    }
}
