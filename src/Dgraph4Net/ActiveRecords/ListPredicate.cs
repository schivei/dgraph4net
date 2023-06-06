using System.Reflection;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct ListPredicate(IClassMap ClassMap, PropertyInfo Property, string PredicateName, string ListType, bool Count = true, bool Reversed = false) : IPredicate
{
    public ISet<IFacet> Facets { get; } = new HashSet<IFacet>();

    public ListPredicate Merge(ListPredicate lpa) =>
        new(ClassMap, Property, PredicateName, ListType, Count || lpa.Count);

    readonly string IPredicate.ToSchemaPredicate() =>
        Reversed ? $"{PredicateName}: uid @reverse {(Count ? "@count" : "")} ." : $"{PredicateName}: [{ListType}] {(Count ? "@count" : "")} .";

    readonly string IPredicate.ToTypePredicate() =>
        Reversed ? $"~{PredicateName}" :
        PredicateName;

    public static ListPredicate operator |(ListPredicate lpa1, ListPredicate lpa2) =>
        lpa1.Merge(lpa2);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            ListPredicate p => Merge(p),
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };

    public void SetValue(object? value, object? target)
    {
        if (value is null)
            return;

        if (value.GetType().IsAssignableTo(Property.PropertyType))
        {
            Property.SetValue(target, value);
        }
        else if (Property.PropertyType.IsEnum && ListType == "string")
        {
            var values = (string[])value;
            var enumValue = (int)Enum.Parse(Property.PropertyType, values[0]);
            for (var i = 1; i < values.Length; i++)
                enumValue += (int)Enum.Parse(Property.PropertyType, values[i]);

            var en = Enum.ToObject(Property.PropertyType, enumValue);

            Property.SetValue(target, en);
        }
        else if (Property.PropertyType.IsEnum && ListType == "int")
        {
            var values = (int[])value;
            var enumValue = values.Sum();

            var en = Enum.ToObject(Property.PropertyType, enumValue);

            Property.SetValue(target, en);
        }
    }
}
