using System;

namespace DGraph4Net.Extensions.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ListAttribute : ADGraphAnnotationAttribute
    {
        public ListAttribute() : base(DGraphType.List) { }

        public ListAttribute(string name) : base(name, DGraphType.List) { }
    }
}
