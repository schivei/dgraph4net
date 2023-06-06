namespace Dgraph4Net.ActiveRecords;

public interface IEdgePredicate : IPredicate
{
    bool Reverse { get; }
}
