using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dgraph4Net.Identity;
using Dgraph4Net.Services;
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
        public void ExpTest()
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
    }
}
