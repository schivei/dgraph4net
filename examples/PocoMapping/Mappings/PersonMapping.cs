using Dgraph4Net.ActiveRecords;
using PocoMapping.Entities;

namespace PocoMapping.Mappings;

internal sealed class PersonMapping : ClassMap<Person>
{
    protected override void Map()
    {
        SetType("Person");

        Uid(x => x.Id);
        Types(x => x.DgraphType);

        String(x => x.Name, "name");

        HasOne(x => x.WorksFor, "works_for", true, true);

        HasOne(x => x.MyBoss, "my_boss", true, true, true);

        HasMany(x => x.BossOf, "my_boss", x => x.MyBoss);
    }
}
