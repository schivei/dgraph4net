using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DGraph4Net.Extensions.Mapper
{
    public partial class FluentMapping<T>
    {
        public void List(Expression<Func<T, IEnumerable<string>>> list)
            => MapList(list);

        public void List<TStruct>(Expression<Func<T, IEnumerable<TStruct>>> list) where TStruct : struct
            => MapList(list);

        public void MapList<TE>(Expression<Func<T, IEnumerable<TE>>> list) {
            var type = _mappedType.Type;
            var propInfo = GetPropertyInfo(list);
        }
    }
}
