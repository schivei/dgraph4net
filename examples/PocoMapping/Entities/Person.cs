using Dgraph4Net;

namespace PocoMapping.Entities;

public class Person : IEntity
{
    public Uid Id { get; set; }
    public string[] DgraphType { get; set; } = Array.Empty<string>();

    public string Name { get; set; }
    public ICollection<Person> BossOf { get; set; } = new List<Person>();
    public Company WorksFor { get; set; }

    public Person? MyBoss { get; set; }
}
