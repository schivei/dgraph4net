#nullable enable

using Dgraph4Net.Core;

namespace Dgraph4Net.ActiveRecords;

internal sealed class DgnMigrationMapping : ClassMap<DgnMigration>
{
    protected override void Map()
    {
        SetType("dgn.migration");
        String(x => x.Name, "dgn.name", token: StringToken.Exact);
        DateTime(x => x.GeneratedAt, "dgn.generated_at", DateTimeToken.Hour);
        DateTime(x => x.AppliedAt, "dgn.applied_at", DateTimeToken.Day);
    }
}
