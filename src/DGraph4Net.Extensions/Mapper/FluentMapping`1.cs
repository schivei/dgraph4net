using System;
using System.Linq.Expressions;
using System.Reflection;
using DGraph4Net.Extensions.DataAnnotations;

namespace DGraph4Net.Extensions.Mapper
{
    public partial class FluentMapping<T>
    {
        public void Predicate(Expression<Func<T, string>> predicate)
            => MapPredicate(predicate);

        public void Predicate<TStruct>(Expression<Func<T, TStruct>> predicate) where TStruct : struct
            => MapPredicate(predicate);

        private void MapPredicate<TE>(Expression<Func<T, TE>> predicate)
        {
            var type = _mappedType.Type;
            var propInfo = GetPropertyInfo(predicate);

            ValidatePredicate(type, propInfo);

            var attr = new PredicateAttribute();



            _mappedType.Properties.Add(new MappedProperty<T>
            {
                DGraphType = DGraphType.Predicate,
                Attribute = attr
            });
        }

        private void ValidatePredicate(PropertyInfo propertyInfo)
        {
            if (propertyInfo != typeof(string) && ) ;
        }
    }
}
