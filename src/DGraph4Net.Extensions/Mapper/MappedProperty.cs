using System;
using System.Reflection;
using DGraph4Net.Extensions.DataAnnotations;

namespace DGraph4Net.Extensions.Mapper
{
    internal class MappedProperty<T>
    {
        public PropertyInfo Property { get; set; }

        public IDGraphAnnotationAttribute Attribute { get; set; }

        public Type OriginType => typeof(T);

        public string RdfName { get; set; }

        public DGraphType DGraphType { get; set; }
    }
}
