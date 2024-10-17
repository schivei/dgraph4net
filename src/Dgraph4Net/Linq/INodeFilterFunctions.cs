using System.Linq.Expressions;

namespace Dgraph4Net;

public interface INodeFilterFunctions
{
    bool Has(string predicate);

    bool Has<T>(Expression<Func<T, object?>> predicate) where T : IEntity =>
        Has(DType<T>.Predicate(predicate));

    bool Uid(params string[] uids);

    bool Uid(params Uid[] uids) =>
        Uid(uids: uids.Select(u => u.ToString()).ToArray());

    bool UidIn(string predicateName, params string[] uids);

    bool UidIn(string predicateName, params Uid[] uids) =>
        UidIn(predicateName, uids: uids.Select(u => u.ToString()).ToArray());

    bool UidIn<T, TE>(Expression<Func<T, TE?>> predicate, params TE[] uids) where T : IEntity =>
        UidIn(DType<T>.Predicate(predicate), uids: uids.Select(u => u.ConcreteUid()).ToArray());

    bool Type<T>() where T : IEntity =>
        Type(DType<T>.Name);

    bool Type(string type);
}
