using System.Linq.Expressions;

namespace Dgraph4Net;

public interface IStringFilterFunctions
{
    bool AllOfTerms(string predicate, string value);

    bool AllOfTerms<T>(Expression<Func<T, object?>> predicate, string value) where T : IEntity =>
        AllOfTerms(DType<T>.Predicate(predicate), value);

    bool AllOfTerms(VarTriple predicate, string value) =>
        AllOfTerms(predicate.Name, value);

    bool AnyOfTerms(string predicate, string value);

    bool AnyOfTerms<T>(Expression<Func<T, object?>> predicate, string value) where T : IEntity =>
        AnyOfTerms(DType<T>.Predicate(predicate), value);

    bool AnyOfTerms(VarTriple predicate, string value) =>
        AnyOfTerms(predicate.Name, value);

    bool Regexp(string predicate, string value);

    bool Regexp<T>(Expression<Func<T, object?>> predicate, string value) where T : IEntity =>
        Regexp(DType<T>.Predicate(predicate), value);

    bool Regexp(VarTriple predicate, string value) =>
        Regexp(predicate.Name, value);

    bool Match(string predicate, string value, uint distance);

    bool Match<T>(Expression<Func<T, object?>> predicate, string value, uint distance) where T : IEntity =>
        Match(DType<T>.Predicate(predicate), value, distance);

    bool Match(VarTriple predicate, string value, uint distance) =>
        Match(predicate.Name, value, distance);

    bool AllOfText(string predicate, string value);

    bool AllOfText<T>(Expression<Func<T, object?>> predicate, string value) where T : IEntity =>
        AllOfText(DType<T>.Predicate(predicate), value);

    bool AllOfText(VarTriple predicate, string value) =>
        AllOfText(predicate.Name, value);
}
