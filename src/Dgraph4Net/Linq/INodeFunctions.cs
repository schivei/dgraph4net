using System.Linq.Expressions;

namespace Dgraph4Net;

public interface INodeFunctions
{
    void Has(string predicate);

    void Has<T>(Expression<Func<T, object?>> predicate) where T : IEntity =>
        Has(DType<T>.Predicate(predicate));

    void Uid(params string[] uids);

    void Uid(params Uid[] uids) =>
        Uid(uids: uids.Select(u => u.ToString()).ToArray());

    void UidIn(string predicateName, params string[] uids);

    void UidIn(string predicateName, params Uid[] uids) =>
        UidIn(predicateName, uids: uids.Select(u => u.ToString()).ToArray());

    void UidIn<T, TE>(Expression<Func<T, TE?>> predicate, params TE[] uids) where T : IEntity =>
        UidIn(DType<T>.Predicate(predicate), uids: uids.Select(u => u.ConcreteUid()).ToArray());

    void Type<T>() where T : IEntity =>
        Type(DType<T>.Name);

    void Type(string type);
}
