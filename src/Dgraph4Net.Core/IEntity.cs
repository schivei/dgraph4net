namespace Dgraph4Net;

public interface IEntityBase { }

public interface IEntity : IEntityBase
{
    public Uid Uid { get; }

    public string[] DgraphType { get; }
}
