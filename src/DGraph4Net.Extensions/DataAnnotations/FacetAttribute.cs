using System;

namespace DGraph4Net.Extensions.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class FacetAttribute : ADGraphAnnotationAttribute
    {
        public FacetAttribute() : base(DGraphType.Facet) { }

        public FacetAttribute(string name) : base(name, DGraphType.Facet) { }
    }
}
