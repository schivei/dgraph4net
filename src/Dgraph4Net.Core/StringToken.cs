using System.Diagnostics.CodeAnalysis;

namespace Dgraph4Net.Annotations
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
