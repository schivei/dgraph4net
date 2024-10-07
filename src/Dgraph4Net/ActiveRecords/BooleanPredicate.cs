using System.Reflection;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct BooleanPredicate(IClassMap ClassMap, PropertyInfo Property, string PredicateName, bool Index = false, bool Upsert = false) : IPredicate
{
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

    public void SetValue<T>(T? target, object? value) where T : IEntity
    {
        if (((IPredicate)this).SetFaceted(target, value))
            return;

        var facetPredicate = typeof(IFacetPredicate<,>);

        if (facetPredicate.IsAssignableFrom(Property.PropertyType) && target is IEntity entity)
        {
            var gen = Property.PropertyType.GetGenericArguments();
            if (gen.Length != 2)
                throw new InvalidOperationException("The property must have two generic types");

            var tp = gen.FirstOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the target");

            var tpe = gen.LastOrDefault() ?? throw new InvalidOperationException("Can not find the generic type of the prop");

            // check if tp is target type
            if (!tp.IsAssignableTo(entity.GetType()))
                throw new InvalidOperationException("The target type must be an IEntity");

            var facet = (IFacetPredicate)(Activator.CreateInstance(typeof(FacetPredicate<,>).MakeGenericType(tp, tpe), entity, Property, value) ??
                throw new InvalidOperationException("Can not create the facet predicate"))!;

            Property.SetValue(entity, facet);
        }

        if (value is bool b)
            Property.SetValue(target, b);
        else if (value is string s && bool.TryParse(s, out var bl))
            Property.SetValue(target, bl);
        else
            Property.SetValue(target, Convert.ChangeType(value, Property.PropertyType));
    }
}
