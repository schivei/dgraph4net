using System.Diagnostics.CodeAnalysis;

namespace Dgraph4Net
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
