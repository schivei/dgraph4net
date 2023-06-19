using Dgraph4Net.ActiveRecords;
using PocoMapping.Entities;

namespace PocoMapping.Migrations;

internal sealed class Testc8c5447b0cf444608c9962945da22f48_20230525184620 : Migration
{
    protected override void Up()
    {
        SetType<Person>();
        SetType<Company>();
        DropPredicate("boss_of");
    }

    protected override void Down()
    {
        DropType("Person");
        DropType("Company");
    }
}
