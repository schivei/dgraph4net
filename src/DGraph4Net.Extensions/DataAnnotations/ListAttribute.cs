using System;

namespace DGraph4Net.Extensions.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ListAttribute : Attribute, IDGraphAnnotationAttribute
    {
        public DGraphType DGraphType { get; }

        public string Name { get; }

        public bool Initialized { get; internal set; }

        public ListAttribute()
        {
            DGraphType = DGraphType.List;
        }

        public ListAttribute(string name) : this()
        {
            Name = name;
            Initialized = true;
        }
    }
}
