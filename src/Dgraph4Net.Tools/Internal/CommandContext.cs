// Copyright (c) .NET Foundation. All rights reserved.

using Dgraph4Net.Tools.Internals;

namespace Dgraph4Net.Tools.Internal
{
    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    internal class CommandContext
    {
        public CommandContext(
            IReporter reporter,
            IConsole console)
        {
            Reporter = reporter;
            Console = console;
        }

        public IConsole Console { get; }
        public IReporter Reporter { get; }
    }
}
