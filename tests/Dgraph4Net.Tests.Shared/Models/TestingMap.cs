using Dgraph4Net.ActiveRecords;

namespace Dgraph4Net.Tests;

public sealed class TestingMap : ClassMap<Testing>
{
    protected override void Map()
    {
        SetType("Testing");
        String(x => x.Name, "name");
        HasOne(x => x.Test, "parent");
        ListString(x => x.Ways, "ways");
    }
}

public sealed class Testing2Map : ClassMap<Testing2>
{
    protected override void Map()
    {
        SetType("Testing2");
        Vector(x => x.Vector, "t2.vector");
        Geo(x => x.MultiPoint, "t2.multiPoint");
    }
}
