using System;

namespace DGraph4Net.Extensions.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class PredicateAttribute : ADGraphAnnotationAttribute
    {
        public PredicateAttribute() : base(DGraphType.Predicate) { }

        public PredicateAttribute(string name) : base(name, DGraphType.Predicate) { }
    }
}
