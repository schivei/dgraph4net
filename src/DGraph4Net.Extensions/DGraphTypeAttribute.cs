using System;
using System.Diagnostics.CodeAnalysis;

namespace DGraph4Net.Annotations
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    [SuppressMessage("ReSharper", "RedundantAttributeUsageProperty")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public sealed class DGraphTypeAttribute : Attribute
    {
        public DGraphTypeAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; set; }
    }
}
