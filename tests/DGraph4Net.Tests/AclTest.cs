using System;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Xunit;

namespace Dgraph4Net.Tests
{
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
        public async Task ExpTest()
        {
            try
            {
                Uid id = "<0x1>";
                Uid refs = "_:0";

                var tes = JsonConvert.SerializeObject(new Testing
                {
                    uid = id,
                    name = "test",
                    TestingId = refs
                });

                var t = JsonConvert.DeserializeObject<Testing>(tes);

                Equal(tes, JsonConvert.SerializeObject(t));
            }
            finally
            {
                await ClearDB();
            }
        }
    }
}
