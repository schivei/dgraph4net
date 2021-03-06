using System;
using System.Diagnostics.CodeAnalysis;

namespace Dgraph4Net.Annotations
{
    /// <summary>
    /// System looks to <see cref="Newtonsoft.Json.JsonPropertyAttribute"/> to get predicate name.
    /// </summary>
    /// <remarks>
    /// If the property is an IEnumerable (expt. by KeyValue or Dictionary), thats marked as list and count automatic.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    [SuppressMessage("ReSharper", "RedundantAttributeUsageProperty")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public sealed class PasswordPredicateAttribute : Attribute
    {
    }
}
