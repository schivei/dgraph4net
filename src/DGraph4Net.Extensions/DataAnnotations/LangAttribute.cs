using System;

namespace DGraph4Net.Extensions.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class LangAttribute : Attribute, IDGraphAnnotationAttribute
    {
        public DGraphType DGraphType { get; }

        public string Name { get; }

        public bool Initialized { get; internal set; }

        public LangAttribute()
        {
            DGraphType = DGraphType.Lang;
        }

        public LangAttribute(string name) : this()
        {
            Name = name;
            Initialized = true;
        }
    }
}
