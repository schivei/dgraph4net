using System;

namespace DGraph4Net.Annotations
{
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class FacetAttribute : Attribute
    {
        public string Name { get; set; }

        public FacetAttribute(string name)
        {
            Name = name;
        }
    }
}
