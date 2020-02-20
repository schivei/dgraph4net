using System;

namespace DGraph4Net.Extensions.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ReferenceAttribute : ADGraphAnnotationAttribute
    {
        public ReferenceAttribute() : base(DGraphType.Reference) { }

        public ReferenceAttribute(string name) : base(name, DGraphType.Reference) { }
    }
}
