using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using DGraph4Net.Extensions.DataAnnotations;

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

        private string NormalizeName(DGraphType predicate, PropertyInfo propInfo)
        {
            throw new NotImplementedException();
        }

        private void FillAttribute<TAtt>(PropertyInfo prop, TAtt att) where TAtt : Attribute
        {
            var potd = new PropertyOverridingTypeDescriptor(TypeDescriptor.GetProvider(prop.DeclaringType).GetTypeDescriptor(prop.DeclaringType));
            var pd = TypeDescriptor.GetProperties(prop.DeclaringType).Find(prop.Name, false);
            var npd = TypeDescriptor.CreateProperty(prop.DeclaringType, pd, att);
            potd.OverrideProperty(npd);
            TypeDescriptor.AddProvider(new TypeDescriptorOverridingProvider(potd), prop.DeclaringType);
        }

        public FluentMapping() :
            this(Regex.Replace(typeof(T).Name, "([^a-zA-Z0-9_])", ""))
        { }

        public static void AutoMap()
        {
            ;
        }
    }

    public class PropertyOverridingTypeDescriptor : CustomTypeDescriptor
    {
        private readonly Dictionary<string, PropertyDescriptor> overridePds = new Dictionary<string, PropertyDescriptor>();

        public PropertyOverridingTypeDescriptor(ICustomTypeDescriptor parent)
            : base(parent)
        { }

        public void OverrideProperty(PropertyDescriptor pd)
        {
            overridePds[pd.Name] = pd;
        }

        public override object GetPropertyOwner(PropertyDescriptor pd)
        {
            object o = base.GetPropertyOwner(pd);

            if (o == null)
            {
                return this;
            }

            return o;
        }

        public PropertyDescriptorCollection GetPropertiesImpl(PropertyDescriptorCollection pdc)
        {
            List<PropertyDescriptor> pdl = new List<PropertyDescriptor>(pdc.Count + 1);

            foreach (PropertyDescriptor pd in pdc)
            {
                if (overridePds.ContainsKey(pd.Name))
                {
                    pdl.Add(overridePds[pd.Name]);
                }
                else
                {
                    pdl.Add(pd);
                }
            }

            PropertyDescriptorCollection ret = new PropertyDescriptorCollection(pdl.ToArray());

            return ret;
        }

        public override PropertyDescriptorCollection GetProperties()
        {
            return GetPropertiesImpl(base.GetProperties());
        }
        public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            return GetPropertiesImpl(base.GetProperties(attributes));
        }
    }

    public class TypeDescriptorOverridingProvider : TypeDescriptionProvider
    {
        private readonly ICustomTypeDescriptor ctd;

        public TypeDescriptorOverridingProvider(ICustomTypeDescriptor ctd)
        {
            this.ctd = ctd;
        }

        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
        {
            return ctd;
        }
    }
}
