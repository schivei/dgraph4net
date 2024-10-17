using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Dgraph4Net.ActiveRecords;

[JsonConverter(typeof(EntityConverter))]
public abstract class AEntity<T> : IEntity where T : AEntity<T>
{
    [IgnoreMapping]
    public object? this[FacetInfo index]
    {
        get => Facets.TryGetValue(index, out var value) ? value : default;
        set => SetFacet(index, value);
    }

    public virtual Uid Uid { get; set; }

    public virtual string[] DgraphType { get; set; }

    [IgnoreMapping]
    public IDictionary<FacetInfo, object?> Facets { get; }

    public AEntity()
    {
        Uid = Uid.NewUid();

        Facets = new Dictionary<FacetInfo, object?>();

        DgraphType = [DType<T>.Name];
    }

    public void SetFacet<TE>(Expression<Func<TE, object?>> predicate, string name, object? value) where TE : IEntity
    {
        var prop = ClassMap.GetProperty<T>(predicate) ??
            throw new ArgumentException($"Can not find property from {predicate}");

        var prep = ClassMap.GetPredicate(prop) ??
            throw new ArgumentException($"Can not find predicate from {prop}");

        var sep = '|';

        if (name is ['@', ..])
        {
            sep = '@';
            name = name[1..];
        }

        SetFacet($"{prep.PredicateName}{sep}{name}", value);
    }

    public void SetFacet(Expression<Func<T, object?>> predicate, string name, object? value) =>
        SetFacet<T>(predicate, name, value);

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

    public void RemoveFacet(FacetInfo facet) =>
        Facets.Remove(facet);

    public void SetFacet<TR>(object? value, [CallerMemberName] string name = "")
        where TR : notnull, IComparable, IComparable<TR>, IEquatable<TR>
    {
        var prop = GetType().GetProperty(name);

        var facetAttr = prop?.GetCustomAttribute<FacetAttribute<T>>();

        if (prop is null || facetAttr is null)
            throw new ArgumentException($"Can not find property from {name}");

        if (ClassMap.GetPredicate(facetAttr.Property) is not IPredicate predicate)
            throw new ArgumentException($"Can not find predicate from {facetAttr.Property}");

        var sep = '|';

        if (facetAttr.IsI18n)
            sep = '@';

        SetFacet($"{predicate.PredicateName}{sep}{facetAttr.Name}", value);
    }

    public TR GetFacet<TR>(TR defaultValue = default!, [CallerMemberName] string name = "")
        where TR : notnull, IComparable, IComparable<TR>, IEquatable<TR>
    {
        var prop = GetType().GetProperty(name);

        var facetAttr = prop?.GetCustomAttribute<FacetAttribute<T>>();

        if (prop is null || facetAttr is null)
            return GetFacet(name, defaultValue);

        return GetFacet(facetAttr.Name, facetAttr.Property, defaultValue);
    }

    public TR GetFacet<TR>(string name, PropertyInfo prop, TR defaultValue = default!)
        where TR : notnull, IComparable, IComparable<TR>, IEquatable<TR>
    {
        var predicate = ClassMap.GetPredicate(prop) ??
            throw new ArgumentException($"Can not find predicate from {prop}");

        return Facets.TryGetValue((predicate.PredicateName, name), out var value) && value is TR val ? val : defaultValue;
    }

    public TR GetFacet<TR>(string name, TR defaultValue = default!)
        where TR : notnull, IComparable, IComparable<TR>, IEquatable<TR> =>
        Facets.TryGetValue(name, out var value) && value is TR val ? val :
            Facets.Where(x => x.Key.FacetName == name).Select(x => x.Value).FirstOrDefault() is TR v ? v : defaultValue;

    public TR GetFacet<TE, TR>(string name, Expression<Func<T, TE?>> from, TR defaultValue = default!)
        where TR : notnull, IComparable, IComparable<TR>, IEquatable<TR> =>
        GetFacet<T, TE, TR>(name, from, defaultValue);

    public TR GetFacet<T1, TE, TR>(string name, Expression<Func<T1, TE>> from, TR defaultValue)
        where T1 : IEntity
        where TR : notnull, IComparable, IComparable<TR>, IEquatable<TR> =>
        GetFacet(name, ClassMap.GetProperty<T>(from), defaultValue);
}
