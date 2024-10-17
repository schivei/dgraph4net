using System.Linq.Expressions;

namespace Dgraph4Net;

public interface IConditionalFunctions
{
    void Eq<T, TE>(Expression<Func<T, TE?>> predicate, params TE[] values) where T : IEntity =>
        Eq(DType<T>.Predicate(predicate), values: values);

    void Eq(string predicate, params object[] values);

    void Eq(Action<ICountableFunction> action, ulong value) =>
        Eq(CountableFunction.Perform(action), value);

    void Ge<T, TE>(Expression<Func<T, TE?>> predicate, params TE[] values) where T : IEntity =>
        Ge(DType<T>.Predicate(predicate), values: values);

    void Ge(string predicate, params object[] values);

    void Ge(Action<ICountableFunction> action, ulong value) =>
        Ge(CountableFunction.Perform(action), value);

    void Gt<T, TE>(Expression<Func<T, TE?>> predicate, params TE[] values) where T : IEntity =>
        Gt(DType<T>.Predicate(predicate), values: values);

    void Gt(string predicate, params object[] values);

    void Gt(Action<ICountableFunction> action, ulong value) =>
        Gt(CountableFunction.Perform(action), value);

    void Le<T, TE>(Expression<Func<T, TE?>> predicate, params TE[] values) where T : IEntity =>
        Le(DType<T>.Predicate(predicate), values: values);

    void Le(string predicate, params object[] values);

    void Le(Action<ICountableFunction> action, ulong value) =>
        Le(CountableFunction.Perform(action), value);

    void Lt<T, TE>(Expression<Func<T, TE?>> predicate, params TE[] values) where T : IEntity =>
        Lt(DType<T>.Predicate(predicate), values: values);

    void Lt(string predicate, params object[] values);

    void Lt(Action<ICountableFunction> action, ulong value) =>
        Lt(CountableFunction.Perform(action), value);

    void Between<T, TE>(Expression<Func<T, TE?>> predicate, TE start, TE end) where T : IEntity =>
        Between(DType<T>.Predicate(predicate), start, end);

    void Between(string predicate, object start, object end);

    void Between(Action<ICountableFunction> action, ulong start, ulong end) =>
        Between(CountableFunction.Perform(action), start, end);
}
