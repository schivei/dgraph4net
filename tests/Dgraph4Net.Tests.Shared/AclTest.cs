using System;
using System.Threading.Tasks;
using Dgraph4Net.ActiveRecords;
using Google.Protobuf;
using LateApexEarlySpeed.Xunit.Assertion.Json;

namespace Dgraph4Net.Tests;

public class AclTest : ExamplesTest
{
    [Fact]
    public async Task ExpTest()
    {
        const string expected = """{ "uid": "0x1","dgraph.type": ["Testing"],"name": "test","parent": { "uid": "_:0","dgraph.type": ["Testing"] } }""";

        try
        {
            Uid id = "0x1";
            Uid refs = "_:0";

            var test = new Testing
            {
                Name = "test",
                Test = new()
            };

            test.Uid.Replace(id);
            test.Test.Uid.Replace(refs);

            var bs = ByteString.CopyFromUtf8(expected);

            var t = bs.FromJson<Testing>();

            Equivalent(test, t);

            var actual = test.ToJsonString();

            JsonAssertion.Equivalent(expected, actual);
        }
        finally
        {
            await ClearDb();
        }
    }
}
