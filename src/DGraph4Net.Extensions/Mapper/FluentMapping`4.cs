using System;
using System.Linq.Expressions;

namespace DGraph4Net.Extensions.Mapper
{
    public partial class FluentMapping<T>
    {
        public void Lang(Expression<Func<T, string>> lang)
            => MapLang(lang);

        public void Lang<TStruct>(Expression<Func<T, TStruct>> lang) where TStruct : struct
            => MapLang(lang);

        public void MapLang<TE>(Expression<Func<T, TE>> lang)
        {
            var type = _mappedType.Type;
            var propInfo = GetPropertyInfo(lang);
        }
    }
}
