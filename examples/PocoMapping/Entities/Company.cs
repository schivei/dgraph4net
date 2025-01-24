using Dgraph4Net.ActiveRecords;

namespace PocoMapping.Entities;

public class Company : AEntity<Company>
{
    public string Name { get; set; }
    public CompanyIndustry Industry { get; set; }
    public ICollection<Person> WorksHere { get; set; } = [];
    public ICollection<Person> WorkedHere { get; set; } = [];

    [Facet<Person>("since", nameof(Person.WorksFor))]
    public DateTimeOffset Since => GetFacet(DateTimeOffset.MinValue);
}
