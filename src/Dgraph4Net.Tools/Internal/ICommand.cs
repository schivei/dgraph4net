// Copyright (c) .NET Foundation. All rights reserved.

namespace Dgraph4Net.Tools.Internal
{
    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    internal interface ICommand
    {
        void Execute(CommandContext context);
    }
}
