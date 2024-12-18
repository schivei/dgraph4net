using System.Linq.Expressions;

namespace Dgraph4Net;

public interface IConditionalFilterFunctions
{
    bool Eq<T, TE>(Expression<Func<T, TE?>> predicate, params TE[] values) where T : IEntity =>
        Eq(DType<T>.Predicate(predicate), values: values);

    bool Eq(string predicate, params object[] values);

    bool Eq(Action<ICountableFunction> action, ulong value) =>
        Eq(CountableFunction.Perform(action), value);

    bool Ge<T, TE>(Expression<Func<T, TE?>> predicate, params TE[] values) where T : IEntity =>
        Ge(DType<T>.Predicate(predicate), values: values);

    bool Ge(string predicate, params object[] values);

    bool Ge(Action<ICountableFunction> action, ulong value) =>
        Ge(CountableFunction.Perform(action), value);

    bool Gt<T, TE>(Expression<Func<T, TE?>> predicate, params TE[] values) where T : IEntity =>
        Gt(DType<T>.Predicate(predicate), values: values);

    bool Gt(string predicate, params object[] values);

    bool Gt(Action<ICountableFunction> action, ulong value) =>
        Gt(CountableFunction.Perform(action), value);

    bool Le<T, TE>(Expression<Func<T, TE?>> predicate, params TE[] values) where T : IEntity =>
        Le(DType<T>.Predicate(predicate), values: values);

    bool Le(string predicate, params object[] values);

    bool Le(Action<ICountableFunction> action, ulong value) =>
        Le(CountableFunction.Perform(action), value);

    bool Lt<T, TE>(Expression<Func<T, TE?>> predicate, params TE[] values) where T : IEntity =>
        Lt(DType<T>.Predicate(predicate), values: values);

    bool Lt(string predicate, params object[] values);

    bool Lt(Action<ICountableFunction> action, ulong value) =>
        Lt(CountableFunction.Perform(action), value);

    bool Between<T, TE>(Expression<Func<T, TE?>> predicate, TE start, TE end) where T : IEntity =>
        Between(DType<T>.Predicate(predicate), start, end);

    bool Between(string predicate, object start, object end);

    bool Between(Action<ICountableFunction> action, ulong start, ulong end) =>
        Between(CountableFunction.Perform(action), start, end);
}
