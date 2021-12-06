using System;
using System.Diagnostics.CodeAnalysis;

namespace Dgraph4Net.Annotations
{
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
