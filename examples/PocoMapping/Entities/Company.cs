using Dgraph4Net;

namespace PocoMapping.Entities;

public class Company : IEntity
{
    public Uid Id { get; set; }
    public string[] DgraphType { get; set; } = Array.Empty<string>();
    public string Name { get; set; }
    public CompanyIndustry Industry { get; set; }
    public ICollection<Person> WorksHere { get; set; } = new List<Person>();
}
