using System.Linq.Expressions;
using NetGeo.Json;

namespace Dgraph4Net;

public interface IGeoFunctions
{
    void Near(string predicate, double latitude, double longitude, long distance);

    void Near<T>(Expression<Func<T, GeoObject>> predicate, double latitude, double longitude, long distance) where T : IEntity =>
        Near(DType<T>.Predicate(predicate), latitude, longitude, distance);

    void Near(VarTriple predicate, double latitude, double longitude, long distance) =>
        Near(predicate.Name, latitude, longitude, distance);

    void Within(string predicate, (double latitude, double longitude)[] points);

    void Within<T>(Expression<Func<T, GeoObject>> predicate, (double latitude, double longitude)[] points) where T : IEntity =>
        Within(DType<T>.Predicate(predicate), points);

    void Within(VarTriple predicate, (double latitude, double longitude)[] points) =>
        Within(predicate.Name, points);

    void Contains(string predicate, double latitude, double longitude);

    void Contains<T>(Expression<Func<T, GeoObject>> predicate, double latitude, double longitude) where T : IEntity =>
        Contains(DType<T>.Predicate(predicate), latitude, longitude);

    void Contains(VarTriple predicate, double latitude, double longitude) =>
        Contains(predicate.Name, latitude, longitude);

    void Contains(string predicate, (double latitude, double longitude)[] points);

    void Contains<T>(Expression<Func<T, GeoObject>> predicate, (double latitude, double longitude)[] points) where T : IEntity =>
        Contains(DType<T>.Predicate(predicate), points);

    void Contains(VarTriple predicate, (double latitude, double longitude)[] points) =>
        Contains(predicate.Name, points);

    void Intersects(string predicate, (double latitude, double longitude)[] points);

    void Intersects<T>(Expression<Func<T, GeoObject>> predicate, (double latitude, double longitude)[] points) where T : IEntity =>
        Intersects(DType<T>.Predicate(predicate), points);

    void Intersects(VarTriple predicate, (double latitude, double longitude)[] points) =>
        Intersects(predicate.Name, points);
}
