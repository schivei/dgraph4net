using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dgraph4Net.Annotations;

namespace Dgraph4Net.OpenIddict.Models;

public abstract class AEntity : IEntity
{
    protected AEntity()
    {
        _dgraphType = new[] { DgraphExtensions.GetDType(this) };
        Id = Guid.NewGuid();
        ExtraData = new();
    }

    [JsonPropertyName("uid")]
    public Uid Id { get; set; }

    private ICollection<string> _dgraphType;

    [JsonPropertyName("dgraph.type")]
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
    public Dictionary<string, JsonElement> ExtraData { get; set; }

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

        ExtraData.Where(x => x.Key.Contains('@') && x.Key.Split('@')[0] == columnName && x.Value.ValueKind != JsonValueKind.Undefined && x.Value.ValueKind != JsonValueKind.Null)
            .ToList().ForEach(x => lss.Add(new() { LocalizedKey = x.Key, Value = x.Value.GetString() }));

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
