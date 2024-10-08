using Dgraph4Net.ActiveRecords;

namespace PocoMapping.Entities;

public class Person : AEntity<Person>
{
    public string Name { get; set; }
    public Company WorksFor { get; set; }
    public Person? MyBoss { get; set; }
    public SalaryFacet Salary { get; set; }
    public ICollection<Person> BossOf { get; set; } = [];
    public ICollection<Company> WorkedAt { get; set; } = [];

    [Facet<Person>("amiable", nameof(Amiable))]
    public bool Amiable
    {
        get => GetFacet(false);
        set => SetFacet<bool>(value);
    }
}
