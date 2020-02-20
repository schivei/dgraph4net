using System.Collections.Generic;
using System;

namespace DGraph4Net.Extensions.Mapper
{
    internal class MappedType<T>
    {
        public string TypeName { get; }

        public ICollection<MappedProperty<T>> Properties { get; }

        public Type Type { get; }

        public MappedType(string typeName)
        {
            TypeName = typeName;
            Properties = new List<MappedProperty<T>>();
            Type = typeof(T);
        }
    }
}
