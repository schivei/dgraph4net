using System.Linq.Expressions;

namespace Dgraph4Net;

public interface IValueFunctions
{
    string Value<T>(Expression<Func<T, object?>> predicate) where T : IEntity =>
        Value(DType<T>.Predicate(predicate));

    string Value(string predicate) =>
        $"val({predicate})";

    string Value(VarTriple triple) =>
        Value(triple.Name);
}
