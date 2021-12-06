// Copyright (c) .NET Foundation. All rights reserved.

using System;
using System.Linq;
using System.Reflection;

using Dgraph4Net.Tools.Internal;

using Microsoft.Extensions.CommandLineUtils;

namespace Dgraph4Net.Tools.Internals
{
    internal static class CommandLineApplicationExtensions
    {
        public static CommandOption HelpOption(this CommandLineApplication app)
            => app.HelpOption("-?|-h|--help");

        public static CommandOption VerboseOption(this CommandLineApplication app)
            => app.Option("-v|--verbose", "Show verbose output", CommandOptionType.NoValue, inherited: true);

        public static void OnExecute(this CommandLineApplication app, Action action)
            => app.OnExecute(() =>
            {
                action();
                return 0;
            });

        public static CommandOption Option(this CommandLineApplication command, string template, string description)
            => command.Option(
                template,
                description,
                template.IndexOf("<", StringComparison.Ordinal) != -1
                    ? template.EndsWith(">...", StringComparison.Ordinal) ? CommandOptionType.MultipleValue : CommandOptionType.SingleValue
                    : CommandOptionType.NoValue);

        public static void VersionOptionFromAssemblyAttributes(this CommandLineApplication app)
            => app.VersionOptionFromAssemblyAttributes(typeof(CommandLineApplicationExtensions).Assembly);

        public static void VersionOptionFromAssemblyAttributes(this CommandLineApplication app, Assembly assembly)
            => app.VersionOption("--version", GetInformationalVersion(assembly));

        private static string GetInformationalVersion(Assembly assembly)
        {
            var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            var versionAttribute = attribute == null
                ? assembly.GetName().Version.ToString()
                : attribute.InformationalVersion;

            return versionAttribute;
        }
    }
}
