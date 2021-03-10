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
    public sealed class GeoPredicateAttribute : Attribute
    {
        private bool _upsert;

        public bool Index { get; set; }

        public bool Upsert
        {
            get => _upsert;
            set
            {
                if (value)
                    Index = true;

                _upsert = value;
            }
        }

        public void Merge(GeoPredicateAttribute spa)
        {
            Index |= spa.Index;
            Upsert |= spa.Upsert;
        }

        public static GeoPredicateAttribute operator |(GeoPredicateAttribute spa1, GeoPredicateAttribute spa2)
        {
            spa1.Merge(spa2);
            return spa1;
        }
    }
}
