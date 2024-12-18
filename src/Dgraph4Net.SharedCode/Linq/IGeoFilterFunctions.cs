using System.Linq.Expressions;
using NetGeo.Json;

namespace Dgraph4Net;

public interface IGeoFilterFunctions
{
    bool Near(string predicate, double latitude, double longitude, long distance);

    bool Near<T>(Expression<Func<T, GeoObject>> predicate, double latitude, double longitude, long distance) where T : IEntity =>
        Near(DType<T>.Predicate(predicate), latitude, longitude, distance);

    bool Near(VarTriple predicate, double latitude, double longitude, long distance) =>
        Near(predicate.Name, latitude, longitude, distance);

    bool Within(string predicate, (double latitude, double longitude)[] points);

    bool Within<T>(Expression<Func<T, GeoObject>> predicate, (double latitude, double longitude)[] points) where T : IEntity =>
        Within(DType<T>.Predicate(predicate), points);

    bool Within(VarTriple predicate, (double latitude, double longitude)[] points) =>
        Within(predicate.Name, points);

    bool Contains(string predicate, double latitude, double longitude);

    bool Contains<T>(Expression<Func<T, GeoObject>> predicate, double latitude, double longitude) where T : IEntity =>
        Contains(DType<T>.Predicate(predicate), latitude, longitude);

    bool Contains(VarTriple predicate, double latitude, double longitude) =>
        Contains(predicate.Name, latitude, longitude);

    bool Contains(string predicate, (double latitude, double longitude)[] points);

    bool Contains<T>(Expression<Func<T, GeoObject>> predicate, (double latitude, double longitude)[] points) where T : IEntity =>
        Contains(DType<T>.Predicate(predicate), points);

    bool Contains(VarTriple predicate, (double latitude, double longitude)[] points) =>
        Contains(predicate.Name, points);

    bool Intersects(string predicate, (double latitude, double longitude)[] points);

    bool Intersects<T>(Expression<Func<T, GeoObject>> predicate, (double latitude, double longitude)[] points) where T : IEntity =>
        Intersects(DType<T>.Predicate(predicate), points);

    bool Intersects(VarTriple predicate, (double latitude, double longitude)[] points) =>
        Intersects(predicate.Name, points);
}
