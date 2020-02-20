using System;

namespace DGraph4Net.Extensions.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ReferenceAttribute : Attribute, IDGraphAnnotationAttribute
    {
        public DGraphType DGraphType { get; }

        public string Name { get; }

        public bool Initialized { get; internal set; }

        public ReferenceAttribute()
        {
            DGraphType = DGraphType.Reference;
        }

        public ReferenceAttribute(string name) : this()
        {
            Name = name;
            Initialized = true;
        }
    }
}
