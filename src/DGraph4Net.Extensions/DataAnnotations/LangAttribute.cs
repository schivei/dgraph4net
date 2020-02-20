using System;

namespace DGraph4Net.Extensions.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class LangAttribute : ADGraphAnnotationAttribute
    {
        public LangAttribute() : base(DGraphType.Lang) { }

        public LangAttribute(string name) : base(name, DGraphType.Lang) { }
    }
}
