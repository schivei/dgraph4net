using Dgraph4Net.ActiveRecords;
using PocoMapping.Entities;

namespace PocoMapping.Migrations;

internal sealed class Teste199e45d7e1e4d538306bc9c61acc4a8_20230525175626 : Migration
{
    protected override void Up()
    {
        SetType<Company>();
        SetType<Person>();
    }

    protected override void Down()
    {
        DropType("Company");
        DropType("Person");
    }
}
