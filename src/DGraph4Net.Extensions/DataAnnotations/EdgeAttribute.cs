using System;

namespace DGraph4Net.Extensions.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class EdgeAttribute : Attribute, IDGraphAnnotationAttribute
    {
        public DGraphType DGraphType { get; }

        public string Name { get; }

        public bool Initialized { get; internal set; }

        public EdgeAttribute()
        {
            DGraphType = DGraphType.Edge;
        }

        public EdgeAttribute(string name) : this()
        {
            Name = name;
            Initialized = true;
        }
    }
}
