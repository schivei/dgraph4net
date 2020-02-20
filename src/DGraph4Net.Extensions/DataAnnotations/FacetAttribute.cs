using System;

namespace DGraph4Net.Extensions.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class FacetAttribute : Attribute, IDGraphAnnotationAttribute
    {
        public DGraphType DGraphType { get; }

        public string Name { get; }

        public bool Initialized { get; internal set; }

        public FacetAttribute()
        {
            DGraphType = DGraphType.Facet;
        }

        public FacetAttribute(string name) : this()
        {
            Name = name;
            Initialized = true;
        }
    }
}
