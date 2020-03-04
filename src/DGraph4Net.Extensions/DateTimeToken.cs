using System.Diagnostics.CodeAnalysis;

namespace Dgraph4Net.Annotations
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum DateTimeToken
    {
        None,
        Year,
        Month,
        Day,
        Hour
    }
}
