namespace Dgraph4Net.ActiveRecords;

/// <summary>
/// Represents a property that should be ignored when mapping.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class IgnoreMappingAttribute : Attribute { }
