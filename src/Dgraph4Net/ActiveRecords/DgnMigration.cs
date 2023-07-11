
namespace Dgraph4Net.ActiveRecords;

internal sealed class DgnMigration : IDgnMigration
{
    public DgnMigration() { }

    public DgnMigration(Migration migration)
    {
        Name = migration.Name;
        GeneratedAt = migration.GeneratedAt;
    }

    public string[] DgraphType { get; set; } = Array.Empty<string>();
    public Uid Uid { get; set; }

    public DateTimeOffset AppliedAt { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public string Name { get; set; }
}
