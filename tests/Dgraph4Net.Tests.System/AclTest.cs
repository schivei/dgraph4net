using System;
using System.Text.Json.Serialization;
using Dgraph4Net.ActiveRecords;
using Google.Protobuf;
using LateApexEarlySpeed.Xunit.Assertion.Json;

namespace Dgraph4Net.Tests;

public sealed class Testing : AEntity<Testing>
{
    [JsonPropertyName("uid")]
    public override Uid Uid { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("parent")]
    public Testing? Test { get; set; }
    [JsonPropertyName("dgraph.type")]
    public override string[] DgraphType { get; set; } = new[] { "Testing" };
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
        const string expected = @"{ ""uid"": ""0x1"",""dgraph.type"": [""Testing""],""name"": ""test"",""parent"": { ""uid"": ""_:0"",""dgraph.type"": [""Testing""],""name"": null,""parent"": null } }" + "\n";

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

            var bs = ByteString.CopyFromUtf8(expected);

            var t = bs.FromJson<Testing>();

            Equivalent(test, t);

            var actual = ClassMapping.ToJson(test).ToStringUtf8().Trim();

            var expec = expected.Trim();

            Console.WriteLine(actual);

            JsonAssertion.Equivalent(expec, actual);
        }
        finally
        {
            //await ClearDB();
        }
    }
}
