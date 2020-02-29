using System.Diagnostics.CodeAnalysis;

namespace DGraph4Net.Annotations
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum StringToken
    {
        None,
        Exact,
        Hash,
        Term
    }
}
