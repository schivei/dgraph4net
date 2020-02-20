using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DGraph4Net.Extensions.DataAnnotations;
using GeoJSON.Net.Geometry;

namespace DGraph4Net.Extensions.Mapper
{
    public partial class FluentMapping<T>
    {
        public void Predicate(Expression<Func<T, string>> predicate, string name = null)
            => MapPredicate(predicate, name);

        public void Predicate<TStruct>(Expression<Func<T, TStruct>> predicate, string name = null) where TStruct : struct
            => MapPredicate(predicate, name);

        private void MapPredicate<TE>(Expression<Func<T, TE>> predicate, string name)
        {
            var propInfo = GetPropertyInfo(predicate);

            ValidatePredicate(propInfo);

            ADGraphAnnotationAttribute attr = new PredicateAttribute();

            var attrs = propInfo.GetCustomAttributes().Where(att => (att is ADGraphAnnotationAttribute)).OfType<ADGraphAnnotationAttribute>().ToArray();

            if (attrs.Length > 0 && !attrs.Any(att => att is PredicateAttribute))
                throw new InvalidOperationException($"The property isn't a Predicate it's a {attrs[0].DGraphType}.");
            else if (attrs.Length > 0)
                attr = attrs[0];

            name = attr.Name ?? name;

            if (!attr.Initialized || string.IsNullOrEmpty(attr.Name?.Trim()))
                name ??= attr.Name ?? NormalizeName(DGraphType.Predicate, propInfo);

            ValidatePredicateName(name);
            attr.SetName(name);

            FillAttribute(propInfo, attr);

            _mappedType.Properties.Add(new MappedProperty<T>
            {
                DGraphType = DGraphType.Predicate,
                Attribute = attr,
                Property = propInfo,
                RdfName = name
            });
        }

        private void ValidatePredicateName(string name)
        {
            if (string.IsNullOrEmpty(name?.Trim()))
                throw new ArgumentNullException(nameof(name));
        }

        private void ValidatePredicate(PropertyInfo propertyInfo)
        {
            var isValidSystemType = (Type.GetTypeCode(propertyInfo.PropertyType)) switch
            {
                TypeCode.Boolean => true,
                TypeCode.Byte => true,
                TypeCode.Char => true,
                TypeCode.DateTime => true,
                TypeCode.Decimal => true,
                TypeCode.Double => true,
                TypeCode.Int16 => true,
                TypeCode.Int32 => true,
                TypeCode.Int64 => true,
                TypeCode.SByte => true,
                TypeCode.Single => true,
                TypeCode.String => true,
                TypeCode.UInt16 => true,
                TypeCode.UInt32 => true,
                TypeCode.UInt64 => true,

                TypeCode.Empty => false,
                TypeCode.DBNull => false,
                TypeCode.Object => false,
                _ => false,
            };

            if (!isValidSystemType &&
                !propertyInfo.PropertyType.IsEnum &&
                propertyInfo.PropertyType != typeof(Uid) &&
                propertyInfo.PropertyType != typeof(DateTimeOffset) &&
                propertyInfo.PropertyType != typeof(TimeSpan) &&
                propertyInfo.PropertyType != typeof(Guid) &&
                propertyInfo.PropertyType != typeof(byte[]) &&
                propertyInfo.PropertyType != typeof(char[]) &&
                propertyInfo.PropertyType != typeof(Point) &&
                propertyInfo.PropertyType != typeof(LineString) &&
                propertyInfo.PropertyType != typeof(Polygon) &&
                propertyInfo.PropertyType != typeof(MultiPoint) &&
                propertyInfo.PropertyType != typeof(MultiLineString) &&
                propertyInfo.PropertyType != typeof(MultiPolygon) &&
                propertyInfo.PropertyType != typeof(GeometryCollection))
            {
                throw new ArgumentException($"The property {typeof(T).FullName}::{propertyInfo.Name} is not a valid predicate.", nameof(propertyInfo));
            }
        }
    }
}
