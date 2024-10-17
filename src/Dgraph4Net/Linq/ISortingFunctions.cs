using System.Linq.Expressions;

namespace Dgraph4Net;

public interface ISortingFunctions
{
    internal string SortBy { get; set; }
    internal SortingType Order { get; set; }

    void Asc(string predicate) =>
        (SortBy, Order) = (predicate, SortingType.asc);

    void Asc<T>(Expression<Func<T, object?>> predicate) where T : IEntity =>
        Asc(DType<T>.Predicate(predicate));

    void Asc(VarTriple triple) =>
        Asc(triple.Name);

    void Asc<T>(Expression<Func<T, object?>> predicate, string lang) where T : IEntity =>
        Asc(DType<T>.Predicate(predicate) + '@' + lang);
    void Desc(string predicate) =>
        (SortBy, Order) = (predicate, SortingType.desc);

    void Desc<T>(Expression<Func<T, object?>> predicate) where T : IEntity =>
        Desc(DType<T>.Predicate(predicate));

    void Desc(VarTriple triple) =>
        Desc(triple.Name);

    void Desc<T>(Expression<Func<T, object?>> predicate, string lang) where T : IEntity =>
        Desc(DType<T>.Predicate(predicate) + '@' + lang);
}
