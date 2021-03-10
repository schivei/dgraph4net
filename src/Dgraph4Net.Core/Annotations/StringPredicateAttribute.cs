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
    public sealed class StringPredicateAttribute : Attribute
    {
        public bool Fulltext { get; set; }

        public bool Trigram { get; set; }

        public bool Upsert { get; set; }

        public StringToken Token { get; set; }

        public bool Lang { get; set; }

        public void Merge(StringPredicateAttribute spa)
        {
            Fulltext |= spa.Fulltext;
            Trigram |= spa.Trigram;
            Upsert |= spa.Upsert;
            Token = spa.Token != StringToken.None ? spa.Token : Token;
            Lang = spa.Lang;
        }

        public static StringPredicateAttribute operator |(StringPredicateAttribute spa1, StringPredicateAttribute spa2)
        {
            spa1.Merge(spa2);
            return spa1;
        }
    }
}
