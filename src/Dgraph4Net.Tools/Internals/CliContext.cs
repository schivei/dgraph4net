// Copyright (c) .NET Foundation. All rights reserved.

using System;

namespace Dgraph4Net.Tools.Internals
{
    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    internal static class CliContext
    {
        /// <summary>
        /// dotnet -d|--diagnostics subcommand
        /// </summary>
        /// <returns></returns>
        public static bool IsGlobalVerbose()
        {
            _ = bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE"), out bool globalVerbose);
            return globalVerbose;
        }
    }
}
