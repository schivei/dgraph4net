using Dgraph4Net.ActiveRecords;

namespace PocoMapping.Entities;

public sealed class SalaryFacet(Person instance, decimal value = default) : FacetPredicate<Person, decimal>(instance, property => property.Salary, value)
{
    public string Currency
    {
        get => GetFacet<string>("currency");
        set => SetFacet("currency", value);
    }

    public int PayDay
    {
        get => GetFacet<int>("payday");
        set => SetFacet("payday", value);
    }
}
