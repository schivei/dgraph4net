using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using Dgraph4Net.Annotations;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Dgraph4Net.OpenIddict.Models
{
    public abstract class AEntity : IEntity
    {
        protected AEntity()
        {
            _dgraphType = new[] { DgraphExtensions.GetDType(this) };
            Id = Guid.NewGuid();
            ExtraData = new();
        }

        [JsonProperty("uid")]
        public Uid Id { get; set; }

        private ICollection<string> _dgraphType;

        [JsonProperty("dgraph.type")]
        public ICollection<string> DgraphType
        {
            get
            {
                var dtype = DgraphExtensions.GetDType(this);
                if (_dgraphType.All(dt => dt != dtype))
                    _dgraphType.Add(dtype);

                return _dgraphType;
            }
            set
            {
                var dtype = DgraphExtensions.GetDType(this);
                if (value.All(dt => dt != dtype))
                    value.Add(dtype);

                _dgraphType = value;
            }
        }

        [JsonExtensionData, IgnoreMapping]
        public Dictionary<string, JToken> ExtraData { get; set; }

        protected void PopulateLocalized([CallerMemberName] string memberName = "")
        {
            if (string.IsNullOrEmpty(memberName?.Trim()))
                return;

            var property = GetType().GetProperty(memberName);
            if (property is null)
                return;

            var sla = property.GetPropertyAttribute<StringLanguageAttribute>();

            if (sla is null)
                return;

            var columnName = this.GetColumnName(memberName);

            var lss = new LocalizedStrings();

            ExtraData.Where(x => x.Key.Contains('@') && x.Key.Split('@')[0] == columnName && x.Value.HasValues)
                .ToList().ForEach(x => lss.Add(new() { LocalizedKey = x.Key, Value = x.Value.Value<string>() }));

            property.SetValue(this, lss);
        }

        public static explicit operator Uid(AEntity entity) =>
            entity.Id;
    }

    public abstract class AEntity<TEntity> : AEntity where TEntity : AEntity<TEntity>, new()
    {
        public static explicit operator AEntity<TEntity>(Uid uid) =>
            new TEntity { Id = uid };
    }
}
