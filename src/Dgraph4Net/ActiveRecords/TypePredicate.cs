using System.Reflection;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct TypePredicate(IClassMap ClassMap, PropertyInfo Property) : IPredicate
{
    public string PredicateName => "dgraph.type";
    public ISet<IFacet> Facets { get; } = new HashSet<IFacet>();
    readonly string IPredicate.ToSchemaPredicate() => string.Empty;
    readonly string IPredicate.ToTypePredicate() => PredicateName;

    public static TypePredicate operator |(TypePredicate lpa1, TypePredicate lpa2) =>
        lpa1.Merge(lpa2);

    public TypePredicate Merge(TypePredicate _) =>
        new(ClassMap, Property);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            TypePredicate p => Merge(p),
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };

    public void SetValue(object? value, object? target)
    {
        if (value is null)
            return;

        if (value is IEnumerable<string> ie)
            Property.SetValue(target, ie.ToArray());
    }
}
