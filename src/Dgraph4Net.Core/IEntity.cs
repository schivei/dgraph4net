using System;
using System.Collections.Generic;
using System.Reflection;

using Dgraph4Net.Annotations;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Dgraph4Net
{
    public interface IEntityBase { }

    public interface IEntity : IEntityBase
    {
        public Uid Id { get; set; }

        public ICollection<string> DgraphType { get; set; }
    }

    public class EntityContractResolver : DefaultContractResolver
    {
        public static EntityContractResolver Instance => new EntityContractResolver();

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            var ignore = member.GetCustomAttribute(typeof(IgnoreMappingAttribute));

            if (ignore is null)
            {
                return property;
            }

            property.ShouldSerialize = delegate
            { return false; };

            return property;
        }
    }
}
