using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;

[assembly: InternalsVisibleTo("Dgraph4Net.Identity")]

namespace Dgraph4Net.Identity;

public abstract class AEntity : IEntity
{
    protected AEntity()
    {
        _dgraphType = new[] { DgraphExtensions.GetDType(this) };
        Id = Guid.NewGuid();
    }

    [PersonalData]
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
}
