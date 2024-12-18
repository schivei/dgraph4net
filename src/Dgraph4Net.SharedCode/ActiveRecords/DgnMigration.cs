namespace Dgraph4Net.ActiveRecords;

internal sealed class DgnMigration : AEntity<DgnMigration>, IDgnMigration
{
    public DgnMigration() { }

    public DgnMigration(Migration migration)
    {
        Name = migration.Name;
        GeneratedAt = migration.GeneratedAt;
    }

    public DateTimeOffset AppliedAt { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public string Name { get; set; }
}
