using System.Reflection;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct PasswordPredicate(IClassMap ClassMap, PropertyInfo Property, string PredicateName) : IPredicate
{
    public ISet<IFacet> Facets { get; } = new HashSet<IFacet>();
    readonly string IPredicate.ToSchemaPredicate() =>
        $"{PredicateName}: password .";
    readonly string IPredicate.ToTypePredicate() =>
        PredicateName;

    public static PasswordPredicate operator |(PasswordPredicate lpa1, PasswordPredicate lpa2) =>
        lpa1.Merge(lpa2);

    public PasswordPredicate Merge(PasswordPredicate _) =>
        new(ClassMap, Property, PredicateName);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            PasswordPredicate p => this | p,
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };

    public void SetValue(object? _, object? __) { }
}
