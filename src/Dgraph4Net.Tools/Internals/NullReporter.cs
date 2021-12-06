// Copyright (c) .NET Foundation. All rights reserved.

namespace Dgraph4Net.Tools.Internals
{
    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    internal class NullReporter : IReporter
    {
        private NullReporter()
        { }

        public static IReporter Singleton { get; } = new NullReporter();

        public void Verbose(string message)
        { }

        public void Output(string message)
        { }

        public void Warn(string message)
        { }

        public void Error(string message)
        { }
    }
}
