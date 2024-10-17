using System.Linq.Expressions;

namespace Dgraph4Net;

public interface ICountableFunction
{
    void Count<T>(Expression<Func<T, object?>> predicate) where T : IEntity =>
        Count(TypeExtensions.Predicate(predicate));

    void Count(string predicate);

    void Count(VarTriple triple) => Count(triple.Name);
}
