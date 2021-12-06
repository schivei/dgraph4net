using System;

namespace Dgraph4Net.Annotations
{
    /// <summary>
    /// System looks to <see cref="Newtonsoft.Json.JsonPropertyAttribute"/> to get predicate name.
    /// </summary>
    /// <remarks>
    /// If the property is an IEnumerable (expt. by KeyValue or Dictionary), thats marked as list and count automatic.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class StringLanguageAttribute : Attribute
    {
        public bool Fulltext { get; set; }

        public bool Trigram { get; set; }

        public bool Upsert { get; set; }

        public StringToken Token { get; set; }

        public string CultureOrder { get; set; }

        public string CulturesToFind { get; set; }

        public void Merge(StringLanguageAttribute spa)
        {
            Fulltext |= spa.Fulltext;
            Trigram |= spa.Trigram;
            Upsert |= spa.Upsert;
            Token = spa.Token != StringToken.None ? spa.Token : Token;
        }

        public static StringLanguageAttribute operator |(StringLanguageAttribute spa1, StringLanguageAttribute spa2)
        {
            spa1.Merge(spa2);
            return spa1;
        }
    }
}
