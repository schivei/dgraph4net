using System;

namespace DGraph4Net.Extensions.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class PredicateAttribute : Attribute, IDGraphAnnotationAttribute
    {
        public DGraphType DGraphType { get; }

        public string Name { get; }

        public bool Initialized { get; internal set; }

        public PredicateAttribute()
        {
            DGraphType = DGraphType.Predicate;
        }

        public PredicateAttribute(string name) : this()
        {
            Name = name;
            Initialized = true;
        }
    }
}
