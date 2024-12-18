using System.Linq.Expressions;

namespace Dgraph4Net;

public interface IStringFunctions
{
    void AllOfTerms(string predicate, string value);

    void AllOfTerms<T>(Expression<Func<T, object?>> predicate, string value) where T : IEntity =>
        AllOfTerms(DType<T>.Predicate(predicate), value);

    void AllOfTerms(VarTriple predicate, string value) =>
        AllOfTerms(predicate.Name, value);

    void AnyOfTerms(string predicate, string value);

    void AnyOfTerms<T>(Expression<Func<T, object?>> predicate, string value) where T : IEntity =>
        AnyOfTerms(DType<T>.Predicate(predicate), value);

    void AnyOfTerms(VarTriple predicate, string value) =>
        AnyOfTerms(predicate.Name, value);

    void Regexp(string predicate, string value);

    void Regexp<T>(Expression<Func<T, object?>> predicate, string value) where T : IEntity =>
        Regexp(DType<T>.Predicate(predicate), value);

    void Regexp(VarTriple predicate, string value) =>
        Regexp(predicate.Name, value);

    void Match(string predicate, string value, uint distance);

    void Match<T>(Expression<Func<T, object?>> predicate, string value, uint distance) where T : IEntity =>
        Match(DType<T>.Predicate(predicate), value, distance);

    void Match(VarTriple predicate, string value, uint distance) =>
        Match(predicate.Name, value, distance);

    void AllOfText(string predicate, string value);

    void AllOfText<T>(Expression<Func<T, object?>> predicate, string value) where T : IEntity =>
        AllOfText(DType<T>.Predicate(predicate), value);

    void AllOfText(VarTriple predicate, string value) =>
        AllOfText(predicate.Name, value);
}
