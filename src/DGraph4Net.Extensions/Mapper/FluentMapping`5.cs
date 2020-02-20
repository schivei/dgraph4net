using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace DGraph4Net.Extensions.Mapper
{
    public partial class FluentMapping<T>
    {
        public void Reference(Expression<Func<T, Uid>> lang)
            => MapReference(lang);

        public void Reference(Expression<Func<T, IEnumerable<Uid>>> lang)
            => MapReference(lang);

        public void Reference<TE>(Expression<Func<T, TE>> lang) where TE : class
            => MapReference(lang);

        public void Reference<TE>(Expression<Func<T, IEnumerable<TE>>> lang) where TE : class
            => MapReference(lang);

        public void MapReference<TE>(Expression<Func<T, TE>> lang)
        {
            var type = _mappedType.Type;
            var propInfo = GetPropertyInfo(lang);
        }

        public void MapReference<TE>(Expression<Func<T, IEnumerable<TE>>> lang)
        {
            var type = _mappedType.Type;
            var propInfo = GetPropertyInfo(lang);
        }
    }
}
