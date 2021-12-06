// Copyright (c) .NET Foundation. All rights reserved.

using System;
using System.IO;

namespace Dgraph4Net.Tools.Internals
{
    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    internal interface IConsole
    {
        event ConsoleCancelEventHandler CancelKeyPress;
        TextWriter Out { get; }
        TextWriter Error { get; }
        TextReader In { get; }
        bool IsInputRedirected { get; }
        bool IsOutputRedirected { get; }
        bool IsErrorRedirected { get; }
        ConsoleColor ForegroundColor { get; set; }
        void ResetColor();
    }
}
