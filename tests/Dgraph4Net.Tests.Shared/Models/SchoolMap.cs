using Dgraph4Net.ActiveRecords;

namespace Dgraph4Net.Tests;

public class SchoolMap : ClassMap<School>
{
    protected override void Map()
    {
        SetType("Institution");
        String(x => x.Name, "name");
    }
}
