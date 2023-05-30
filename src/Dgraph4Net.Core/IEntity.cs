using System;

namespace Dgraph4Net;

public interface IEntityBase { }

public interface IEntity : IEntityBase
{
    public Uid Id { get; }

    public string[] DgraphType { get; }
}
