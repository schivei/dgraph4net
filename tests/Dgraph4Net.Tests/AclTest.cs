using System;
using System.Threading.Tasks;

using System.Text.Json.Serialization;
using Xunit;
using System.Text.Json;
using Dgraph4Net.ActiveRecords;
using Google.Protobuf;

namespace Dgraph4Net.Tests;

public sealed class Testing : IEntity
{
    public Uid Id { get; set; }
    public string Name { get; set; }
    public Testing? Test { get; set; }
    public string[] DgraphType { get; set; } = new[] { "Testing" };
}

public sealed class TestingMap : ClassMap<Testing>
{
    protected override void Map()
    {
        SetType("Testing");
        String(x => x.Name, "name");
        HasOne(x => x.Test, "parent");
    }
}

public class AclTest : ExamplesTest
{
    [Fact]
    public void ExpTest()
    {
        const string expected = @"{ ""uid"": ""0x1"",""dgraph.type"": ""Testing"",""name"": ""test"",""parent"": { ""uid"": ""_:0"",""dgraph.type"": ""Testing"",""name"": null,""parent"": null } }" + "\n";

        try
        {
            Uid id = "<0x1>";
            Uid refs = "_:0";

            var test = new Testing
            {
                Id = id,
                Name = "test",
                Test = new(){ Id = refs }
            };

            var t = ByteString.CopyFromUtf8(expected).FromJson<Testing>();

            Equivalent(test, t);

            var actual = test.ToJson(true).ToStringUtf8().Trim();

            var expec = expected.Trim();

            Equal(expec, actual);
        }
        finally
        {
            //await ClearDB();
        }
    }
}
