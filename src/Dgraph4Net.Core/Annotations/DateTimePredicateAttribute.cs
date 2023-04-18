using System;
using System.Diagnostics.CodeAnalysis;

namespace Dgraph4Net.Annotations;

/// <summary>
/// System looks to <see cref="System.Text.Json.Serialization.JsonPropertyNameAttribute"/> to get predicate name.
/// </summary>
/// <remarks>
/// If the property is an IEnumerable (expt. by KeyValue or Dictionary), thats marked as list and count automatic.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class DateTimePredicateAttribute : Attribute
{
    public bool Upsert { get; set; }

    public DateTimeToken Token { get; set; }

    public void Merge(DateTimePredicateAttribute spa)
    {
        Token = spa.Token != DateTimeToken.None ? spa.Token : Token;
        Upsert |= spa.Upsert;
    }

    public static DateTimePredicateAttribute operator |(DateTimePredicateAttribute spa1, DateTimePredicateAttribute spa2)
    {
        spa1.Merge(spa2);
        return spa1;
    }
}
