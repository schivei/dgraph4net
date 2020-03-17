using System;
using System.Diagnostics.CodeAnalysis;

namespace Dgraph4Net.Annotations
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    [SuppressMessage("ReSharper", "RedundantAttributeUsageProperty")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public sealed class DgraphTypeAttribute : Attribute
    {
        public DgraphTypeAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; set; }
    }
}
