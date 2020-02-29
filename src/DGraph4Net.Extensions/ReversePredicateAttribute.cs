using System;
using System.Diagnostics.CodeAnalysis;

namespace DGraph4Net.Annotations
{
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    [SuppressMessage("ReSharper", "RedundantAttributeUsageProperty")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public sealed class ReversePredicateAttribute : Attribute
    {
    }
}
