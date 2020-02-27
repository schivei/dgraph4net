using System;

namespace DGraph4Net.Annotations
{
    public enum StringToken
    {
        None,
        Exact,
        Hash,
        Term
    }

    public enum DateTimeToken
    {
        None,
        Year,
        Month,
        Day,
        Hour
    }

    /// <summary>
    /// System looks to <see cref="Newtonsoft.Json.JsonPropertyAttribute"/> to get predicate name.
    /// </summary>
    /// <remarks>
    /// If the property is an IEnumerable (expt. by KeyValue or Dictionary), thats marked as list and count automatic.
    /// </remarks>
    // ReSharper disable once RedundantAttributeUsageProperty
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class StringPredicateAttribute : Attribute
    {
        public bool Fulltext { get; set; }

        public bool Trigram { get; set; }

        public bool Upsert { get; set; }

        public StringToken Token { get; set; }

        public bool Lang { get; set; }
    }

    /// <summary>
    /// System looks to <see cref="Newtonsoft.Json.JsonPropertyAttribute"/> to get predicate name.
    /// </summary>
    /// <remarks>
    /// If the property is an IEnumerable (expt. by KeyValue or Dictionary), thats marked as list and count automatic.
    /// </remarks>
    // ReSharper disable once RedundantAttributeUsageProperty
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class PasswordPredicateAttribute : Attribute
    {
    }

    /// <summary>
    /// System looks to <see cref="Newtonsoft.Json.JsonPropertyAttribute"/> to get predicate name.
    /// </summary>
    /// <remarks>
    /// If the property is an IEnumerable (expt. by KeyValue or Dictionary), thats marked as list and count automatic.
    /// </remarks>
    // ReSharper disable once RedundantAttributeUsageProperty
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class CommonPredicateAttribute : Attribute
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
    }

    /// <summary>
    /// System looks to <see cref="Newtonsoft.Json.JsonPropertyAttribute"/> to get predicate name.
    /// </summary>
    /// <remarks>
    /// If the property is an IEnumerable (expt. by KeyValue or Dictionary), thats marked as list and count automatic.
    /// </remarks>
    // ReSharper disable once RedundantAttributeUsageProperty
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class DateTimePredicateAttribute : Attribute
    {
        public bool Upsert { get; set; }

        public DateTimeToken Token { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class ReversePredicateAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class PredicateReferencesToAttribute : Attribute
    {
        public Type RefType { get; }

        public PredicateReferencesToAttribute(Type refType)
        {
            RefType = refType;
        }
    }
}
