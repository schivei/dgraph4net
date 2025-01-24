using System.Reflection;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct EdgePredicate<T>(IClassMap ClassMap, PropertyInfo Property, string PredicateName, bool Reverse, bool Count) : IEdgePredicate
{
    public Type EdgeType => typeof(T);

    readonly string IPredicate.ToSchemaPredicate() =>
        $"{PredicateName}: uid {(Count ? "@count" : "")} {(Reverse ? "@reverse" : "")} .";

    readonly string IPredicate.ToTypePredicate() =>
        PredicateName;

    public static EdgePredicate<T> operator |(EdgePredicate<T> lpa1, EdgePredicate<T> lpa2) =>
        lpa1.Merge(lpa2);

    public EdgePredicate<T> Merge(EdgePredicate<T> lpa) =>
        new(ClassMap, Property, PredicateName, Reverse || lpa.Reverse, Count || lpa.Count);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            EdgePredicate<T> p => Merge(p),
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };

    public void SetValue<TE>(TE? target, object? value) where TE : IEntity
    {
        if (((IPredicate)this).SetFaceted(target, value))
            return;

        Property.SetValue(target, Convert.ChangeType(value, Property.PropertyType));
    }
}
