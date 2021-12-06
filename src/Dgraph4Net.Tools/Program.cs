using System;
using System.IO;

using Dgraph4Net.Tools.Internals;

namespace Dgraph4Net.Tools
{
    internal class Program
    {
        private readonly IConsole _console;
        private readonly string _workingDirectory;

        internal static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            new Program(PhysicalConsole.Singleton, Directory.GetCurrentDirectory()).TryRun(args, out int rc);

            return rc;
        }

        public Program(IConsole console, string workingDirectory)
        {
            _console = console;
            _workingDirectory = workingDirectory;
        }

        public bool TryRun(string[] args, out int returnCode)
        {
            try
            {
                returnCode = RunInternal(args);
                return true;
            }
            catch (Exception exception)
            {
                var reporter = CreateReporter(verbose: true);
                reporter.Verbose(exception.ToString());
                //reporter.Error(Resources.FormatError_Command_Failed(exception.Message));
                returnCode = 1;
                return false;
            }
        }

        internal int RunInternal(params string[] args)
        {
            CommandLineOptions options;
            try
            {
                options = CommandLineOptions.Parse(args, _console);
            }
            catch (/*CommandParsing*/Exception ex)
            {
                CreateReporter(verbose: false).Error(ex.Message);
                return 1;
            }

            if (options == null)
            {
                return 1;
            }

            if (options.IsHelp)
            {
                return 2;
            }

            var reporter = CreateReporter(options.IsVerbose);

            //if (options.Command is InitCommandFactory initCmd)
            //{
            //    initCmd.Execute(new CommandContext(null, reporter, _console), _workingDirectory);
            //    return 0;
            //}

            var context = new Internal.CommandContext(reporter, _console);
            options.Command.Execute(context);
            return 0;
        }

        private IReporter CreateReporter(bool verbose)
            => new ConsoleReporter(_console, verbose, quiet: false);
    }
}
