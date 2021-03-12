using System;

namespace Dgraph4Net.Annotations
{
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class HasFacetsAttribute : Attribute
    {
    }
}
