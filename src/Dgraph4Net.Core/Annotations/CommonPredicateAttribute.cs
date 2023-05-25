using System;

namespace Dgraph4Net.Annotations;

/// <summary>
/// System looks to <see cref="System.Text.Json.Serialization.JsonPropertyNameAttribute"/> to get predicate name.
/// </summary>
/// <remarks>
/// If the property is an IEnumerable (expt. by KeyValue or Dictionary), thats marked as list and count automatic.
/// </remarks>
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

    public void Merge(CommonPredicateAttribute spa)
    {
        Index |= spa.Index;
        Upsert |= spa.Upsert;
    }

    public static CommonPredicateAttribute operator |(CommonPredicateAttribute spa1, CommonPredicateAttribute spa2)
    {
        spa1.Merge(spa2);
        return spa1;
    }
}

[AttributeUsage(AttributeTargets.Enum, Inherited = false, AllowMultiple = true)]
public sealed class EnumPredicateAttribute : Attribute
{
    public string Predicate { get; }
    public bool StoreAsString { get; set; }
    public EnumPredicateAttribute(string predicate)
    {
        Predicate = predicate;
    }
}
