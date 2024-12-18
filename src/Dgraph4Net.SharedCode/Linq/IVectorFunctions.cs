using System.Linq.Expressions;
using System.Numerics;

namespace Dgraph4Net;

public interface IVectorFunctions
{
    void SimilarTo(string predicate, uint top, Vector<float> vector);

    void SimilarTo(string predicate, uint top, float[] vector) =>
        SimilarTo(predicate, top, vector.ToVector());

    void SimilarTo(VarTriple triple, uint top, Vector<float> vector) =>
        SimilarTo(triple.Name, top, vector);

    void SimilarTo(VarTriple triple, uint top, float[] vector) =>
        SimilarTo(triple.Name, top, vector.ToVector());

    void SimilarTo<T>(Expression<Func<T, Vector<float>>> predicate, uint top, float[] vector) where T : IEntity =>
        SimilarTo(DType<T>.Predicate(predicate), top, vector.ToVector());

    void SimilarTo<T>(Expression<Func<T, Vector<float>>> predicate, uint top, Vector<float> vector) where T : IEntity =>
        SimilarTo(DType<T>.Predicate(predicate), top, vector);
}
