namespace Dgraph4Net;

public interface IFilterFunctions : IStringFilterFunctions, IConditionalFilterFunctions, INodeFilterFunctions, IGeoFilterFunctions, IVectorFilterFunctions
{
    VarTriples Variables { get; }
}
