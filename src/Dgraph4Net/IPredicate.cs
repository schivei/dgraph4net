
using System.Reflection;

namespace Dgraph4Net;

public interface IPredicate
{
    PropertyInfo Property { get; }
    IClassMap ClassMap { get; }
    string PredicateName { get; }
    internal string ToSchemaPredicate();
    internal string ToTypePredicate();
    IPredicate Merge(IPredicate p2);
    void SetValue<T>(T? target, object? value) where T : IEntity;
    public void SetFacetValue<T>(string facetName, object? value, T? target) where T : IEntity
    {
        if (target is IEntity entity)
        {
            entity[facetName] = value;
            return;
        }

        if (target is IFacetPredicate facet)
        {
            facet[facetName] = value;
            return;
        }
    }

    internal protected bool SetFaceted<T>(T? target, object? value) where T : IEntity
    {
        if (value is null || target is null)
            return false;

        if (value is IFacetPredicate faceted)
        {
            faceted.PredicateProperty = Property;
            faceted.PredicateInstance = target;
            return true;
        }

        var facetPredicate = typeof(IFacetPredicate<,>);

        if (facetPredicate.IsAssignableFrom(Property.PropertyType) && target is IEntity entity)
        {
            var gen = Property.PropertyType.GetGenericArguments();
            if (gen.Length != 2)
                throw new InvalidOperationException("The property must have two generic types");

            var tp = gen.FirstOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the target");

            var tpe = gen.LastOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the prop");

            IFacetPredicate? facet;

            if (Property.PropertyType.Name == "FacetPredicate`2")
            {
                var facetType = Property.PropertyType.MakeGenericType(tp, tpe);
                facet = (IFacetPredicate)(Activator.CreateInstance(facetType, entity, Property, value) ??
                    throw new InvalidOperationException("Can not create the facet predicate"))!;
            }
            else
            {
                facet = (IFacetPredicate)(Activator.CreateInstance(Property.PropertyType, value) ??
                    throw new InvalidOperationException("Can not create the facet predicate"))!;
            }

            Property.SetValue(entity, facet);

            return true;
        }

        return false;
    }

    internal IDictionary<string, object?> GetFacets(IEntity entity) => entity?.Facets.Where(f => f.Key.PredicateName == PredicateName)
            .ToDictionary(f => f.Key.ToString(), f => f.Value) ?? [];
}
