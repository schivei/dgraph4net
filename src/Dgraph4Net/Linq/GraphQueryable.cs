namespace Dgraph4Net;

internal sealed class GraphQueryable(Dgraph4NetClient client) : IGraphQueryable
{
    Dgraph4NetClient IGraphQueryable.Client { get; } = client;

    ICollection<VarBlock> IGraphQueryable.VarBlocks { get; } = [];
}
