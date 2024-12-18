namespace Dgraph4Net;

public interface IFunctions : IStringFunctions, IConditionalFunctions, INodeFunctions, IGeoFunctions, IVectorFunctions
{
    VarTriples Variables { get; }
}
