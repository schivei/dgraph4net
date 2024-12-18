using System.Linq.Expressions;
using System.Numerics;

namespace Dgraph4Net;

public interface IVectorFilterFunctions
{
    bool SimilarTo(string predicate, uint top, Vector<float> vector);

    bool SimilarTo(string predicate, uint top, float[] vector) =>
        SimilarTo(predicate, top, vector.ToVector());

    bool SimilarTo(VarTriple triple, uint top, Vector<float> vector) =>
        SimilarTo(triple.Name, top, vector);

    bool SimilarTo(VarTriple triple, uint top, float[] vector) =>
        SimilarTo(triple.Name, top, vector.ToVector());

    bool SimilarTo<T>(Expression<Func<T, Vector<float>>> predicate, uint top, float[] vector) where T : IEntity =>
        SimilarTo(DType<T>.Predicate(predicate), top, vector.ToVector());

    bool SimilarTo<T>(Expression<Func<T, Vector<float>>> predicate, uint top, Vector<float> vector) where T : IEntity =>
        SimilarTo(DType<T>.Predicate(predicate), top, vector);
}
