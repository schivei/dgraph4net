using Dgraph4Net.ActiveRecords;

namespace Dgraph4Net.Tests;

internal sealed class Testc8c5447b0cf444608c9962945da22f48_20241010000000 : Migration
{
    protected override void Up()
    {
        SetType<Person>();
        SetType<School>();
        SetType<Testing>();
    }

    protected override void Down()
    {
        DropType("Person");
        DropType("Institution");
        DropType("Testing");
    }
}
