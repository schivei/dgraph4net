#nullable enable

namespace Dgraph4Net.ActiveRecords;

internal sealed class DgnMigrationMapping : ClassMap<DgnMigration>
{
    protected override void Map()
    {
        SetType("dgn.migration");
        Uid(x => x.Id);
        Types(x => x.DgraphType);

        String(x => x.Name, "name", token: StringToken.Term);
        DateTime(x => x.GeneratedAt, "generated_at");
        DateTime(x => x.AppliedAt, "applied_at");
    }
}
