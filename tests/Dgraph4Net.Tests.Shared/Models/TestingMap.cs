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
