// Copyright (c) .NET Foundation. All rights reserved.

using Dgraph4Net.Tools.Internals;

using Microsoft.Extensions.CommandLineUtils;

namespace Dgraph4Net.Tools.Internal
{
    internal class MigrateCommand : ICommand
    {
        public void Execute(CommandContext context)
        {
        }

        internal static void Configure(CommandLineApplication command, CommandLineOptions options)
        {
            command.Description = "Manage database mappings";
            command.HelpOption();

            command.OnExecute(() =>
            {
                options.Command = new MigrateCommand();
            });
        }
    }
}
