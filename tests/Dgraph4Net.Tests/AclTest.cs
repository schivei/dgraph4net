using System;
using Dgraph4Net.ActiveRecords;
using Google.Protobuf;
using LateApexEarlySpeed.Xunit.Assertion.Json;
using Xunit;

namespace Dgraph4Net.Tests;

public sealed class Testing : AEntity<Testing>
{
    public string Name { get; set; }
    public Testing? Test { get; set; }
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

            JsonAssertion.Equivalent(expec, actual);
        }
        finally
        {
            //await ClearDB();
        }
    }
}
