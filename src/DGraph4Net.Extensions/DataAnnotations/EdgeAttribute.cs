using System;

namespace DGraph4Net.Extensions.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class EdgeAttribute : ADGraphAnnotationAttribute
    {
        public EdgeAttribute() : base(DGraphType.Edge) { }

        public EdgeAttribute(string name) : base(name, DGraphType.Edge) { }
    }
}
