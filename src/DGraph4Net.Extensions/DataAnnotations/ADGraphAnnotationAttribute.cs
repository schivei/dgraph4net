using System;

namespace DGraph4Net.Extensions.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public abstract class ADGraphAnnotationAttribute : Attribute, IDGraphAnnotationAttribute
    {
        public DGraphType DGraphType { get; }

        public string Name { get; private set; }

        public bool Initialized { get; private set; }

        protected ADGraphAnnotationAttribute(DGraphType dGraphType)
        {
            DGraphType = dGraphType;
        }

        protected ADGraphAnnotationAttribute(string name, DGraphType dGraphType) : this(dGraphType)
        {
            Name = name;
            Initialized = true;
        }

        internal void SetName(string name)
        {
            Name = name;
            Initialized = true;
        }
    }
}
