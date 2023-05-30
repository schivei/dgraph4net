using System;

namespace Dgraph4Net.ActiveRecords;

public interface IDgnMigration : IEntity
{
    DateTimeOffset AppliedAt { get; }
    DateTimeOffset GeneratedAt { get; }
    string Name { get; }
}
