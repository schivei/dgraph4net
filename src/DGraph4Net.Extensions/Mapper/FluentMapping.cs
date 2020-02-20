using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DGraph4Net.Extensions.Mapper
{
    public partial class FluentMapping<T>
    {
        private readonly MappedType<T> _mappedType;

        internal static ConcurrentDictionary<Type, MappedType<T>> Mappings { get; set; }

        static FluentMapping()
        {
            Mappings = new ConcurrentDictionary<Type, MappedType<T>>();
        }

        public FluentMapping(string typeName)
        {
            _mappedType = new MappedType<T>(typeName);
            Mappings.TryAdd(typeof(T), _mappedType);
        }

        private PropertyInfo GetPropertyInfo<TE>(Expression<Func<T, TE>> lambda)
        {
            var type = _mappedType.Type;

            if (!(lambda.Body is MemberExpression member) ||
                !(member.Member is PropertyInfo propInfo) ||
                propInfo?.ReflectedType is null)
                throw new ArgumentException($"Expression '{lambda}' not refers to a property.");

            if (type != propInfo.ReflectedType && !type.IsSubclassOf(propInfo.ReflectedType))
                throw new ArgumentException($"Expression '{lambda}' is a {type} property member.");

            return propInfo;
        }



        public FluentMapping() :
            this(Regex.Replace(typeof(T).Name, "([^a-zA-Z0-9_])", ""))
        { }

        public static void AutoMap()
        {
            ;
        }
    }
}
