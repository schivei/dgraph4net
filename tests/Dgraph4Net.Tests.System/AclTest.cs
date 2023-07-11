using System;
using System.Threading.Tasks;

using Xunit;
using Dgraph4Net.ActiveRecords;
using Google.Protobuf;
using System.Text.Json.Serialization;

namespace Dgraph4Net.Tests;

public sealed class Testing : IEntity
{
    [JsonPropertyName("uid")]
    public Uid Uid { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("parent")]
    public Testing? Test { get; set; }
    [JsonPropertyName("dgraph.type")]
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
                Uid = id,
                Name = "test",
                Test = new() { Uid = refs }
            };

            var t = ByteString.CopyFromUtf8(expected).FromJson<Testing>();

            Equivalent(test, t);

            var actual = ClassMapping.ToJson(test, true).ToStringUtf8().Trim();

            var expec = expected.Trim();

            Equal(expec, actual);
        }
        finally
        {
            //await ClearDB();
        }
    }
}
