using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace Dgraph4Net.ActiveRecords;

public class FacetPredicate<T, TE>(T instance, PropertyInfo property, TE value = default) : IFacetPredicate<T, TE> where T : AEntity<T>
{
    /// <summary>
    /// Prevents the use of IEnumerable or IEntity as a type for TE
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    static FacetPredicate()
    {
        if (typeof(TE).IsAssignableFrom(typeof(IEntity)))
            throw new ArgumentException("TE can not be an IEntity");

        if (typeof(TE) != typeof(string) && typeof(TE).IsAssignableTo(typeof(IEnumerable)))
            throw new ArgumentException("TE can not be an IEnumerable");
    }

    public FacetPredicate(T instance, Expression<Func<T, object?>> expression, TE value = default) :
        this(instance, GetProperty(expression), value)
    { }

    private static PropertyInfo GetProperty(Expression<Func<T, object?>> expression) =>
        ClassMap.GetProperty<T>(expression) ??
        throw new ArgumentException($"Can not find property from {expression}");

    public object? this[FacetInfo index]
    {
        get => Facets.TryGetValue(index, out var value) ? value : default;
        set => SetFacet(index, value);
    }

    object? IFacetPredicate.PredicateValue
    {
        get => PredicateValue;
        set => PredicateValue = (TE)value!;
    }

    public TE PredicateValue { get; set; } = value;

    object? IFacetPredicate.PredicateInstance
    {
        get => PredicateInstance;
        set => PredicateInstance = (T)value!;
    }

    private T _predicateInstance = instance;

    public T PredicateInstance
    {
        get => _predicateInstance;
        set
        {
            _predicateInstance = value;

            Facets = value.Facets;
        }
    }

    public PropertyInfo PredicateProperty { get; set; } = property;

    public IDictionary<FacetInfo, object?> Facets { get; private set; } = instance?.Facets ?? new Dictionary<FacetInfo, object?>();

    public void SetFacet(FacetInfo facet, object? value)
    {
        if (value is null)
        {
            RemoveFacet(facet);
            return;
        }

        Facets[facet] = value switch
        {
            bool bval => bval,
            DateTime dtval => dtval,
            DateTimeOffset dtoval => dtoval,
            float fval => fval,
            double dval => dval,
            int ival => ival,
            _ => value.ToString()
        };
    }

    public void SetFacet<TR>(string facet, TR? value)
        where TR : notnull, IComparable, IComparable<TR>, IEquatable<TR> =>
        SetFacet(new FacetInfo(ClassMap.GetPredicate(PredicateProperty).PredicateName, facet), value);

    public void RemoveFacet(FacetInfo facet) =>
        Facets.Remove(facet);

    public TR GetFacet<TR>(string name, TR defaultValue = default!)
        where TR : notnull, IComparable, IComparable<TR>, IEquatable<TR>
    {
        var predicate = ClassMap.GetPredicate(PredicateProperty) ??
            throw new ArgumentException($"Can not find predicate {PredicateProperty.Name} in class {PredicateProperty.DeclaringType}");

        var facet = new FacetInfo(predicate.PredicateName, name);

        return Facets.TryGetValue(facet, out var value) && value is TR val ? val : defaultValue;
    }

    public static implicit operator TE(FacetPredicate<T, TE> value) =>
        value.PredicateValue;

    public bool IsDummy() => PredicateInstance is null || PredicateProperty is null;
}
