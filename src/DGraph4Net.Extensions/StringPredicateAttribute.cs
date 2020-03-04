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
    public sealed class StringPredicateAttribute : Attribute
    {
        public bool Fulltext { get; set; }

        public bool Trigram { get; set; }

        public bool Upsert { get; set; }

        public StringToken Token { get; set; }

        public bool Lang { get; set; }
    }
}
