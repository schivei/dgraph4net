using System;

namespace DGraph4Net.Annotations
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class DGraphTypeAttribute : Attribute
    {
        public DGraphTypeAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; set; }
    }
}
