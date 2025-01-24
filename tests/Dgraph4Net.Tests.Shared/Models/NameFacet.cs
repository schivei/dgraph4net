using System.Reflection;
using Dgraph4Net.ActiveRecords;

namespace Dgraph4Net.Tests;

public class NameFacet : FacetPredicate<Person, string>
{
    public string Origin
    {
        get => GetFacet("origin", string.Empty);
        set => SetFacet("origin", value);
    }

    public NameFacet(Person instance, PropertyInfo property, string value = null) : base(instance, property, value)
    {
    }

    public static implicit operator string(NameFacet facet) => facet.PredicateValue;

    public static NameFacet operator +(NameFacet facet, string value)
    {
        facet.PredicateValue = value;
        return facet;
    }
}
