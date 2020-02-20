using System;
using System.Linq.Expressions;

namespace DGraph4Net.Extensions.Mapper
{
    public partial class FluentMapping<T>
    {
        public void Facet(Expression<Func<T, string>> facet)
            => MapFacet(facet);

        public void Facet<TStruct>(Expression<Func<T, TStruct>> facet) where TStruct : struct
            => MapFacet(facet);

        public void MapFacet<TE>(Expression<Func<T, TE>> facet)
        {
            var type = _mappedType.Type;
            var propInfo = GetPropertyInfo(facet);
        }
    }
}
