using System.Reflection;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct BooleanPredicate(IClassMap ClassMap, PropertyInfo Property, string PredicateName, bool Index = false, bool Upsert = false) : IPredicate
{
    public ISet<IFacet> Facets { get; } = new HashSet<IFacet>();

    readonly string IPredicate.ToSchemaPredicate() =>
        $"{PredicateName}: bool {(Index || Upsert ? "@index(bool)" : "")} {(Upsert ? "@upsert" : "")} .";

    readonly string IPredicate.ToTypePredicate() =>
        PredicateName;

    public static BooleanPredicate operator |(BooleanPredicate lpa1, BooleanPredicate lpa2) =>
        lpa1.Merge(lpa2);

    public BooleanPredicate Merge(BooleanPredicate lpa) =>
        new(ClassMap, Property, PredicateName, Index || lpa.Index, Upsert || lpa.Upsert);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            BooleanPredicate p => Merge(p),
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };

    public void SetValue(object? value, object? target)
    {
        if (value is null)
            return;

        if (value is bool b)
            Property.SetValue(target, b);
        else if (value is string s && bool.TryParse(s, out var bl))
            Property.SetValue(target, bl);
        else
            Property.SetValue(target, Convert.ChangeType(value, Property.PropertyType));
    }
}
