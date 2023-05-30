using System;
using System.Threading.Tasks;

using System.Text.Json.Serialization;
using Xunit;
using System.Text.Json;

namespace Dgraph4Net.Tests;

public class AclTest : ExamplesTest
{
    public class Testing
    {
        public Uid uid { get; set; }
        public string name { get; set; }

        public Uid TestingId { get; set; }

        public Testing Test { get; set; }
    }

    [Fact]
    public void ExpTest()
    {
        try
        {
            Uid id = "<0x1>";
            Uid refs = "_:0";

            var tes = JsonSerializer.Serialize(new Testing
            {
                uid = id,
                name = "test",
                TestingId = refs
            });

            var t = JsonSerializer.Deserialize<Testing>(tes);

            Equal(tes, JsonSerializer.Serialize(t));
        }
        finally
        {
            //await ClearDB();
        }
    }
}
