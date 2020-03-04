using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dgraph4Net.Identity;
using Dgraph4Net.Services;
using Xunit;

namespace Dgraph4Net.Tests
{
    public class AclTest : ExamplesTest
    {
        [Fact]
        public void ExpTest()
        {
            new Dgraph4NetClient(new Dgraph.DgraphClient[0])
                .Query<DUser>(u => u.Roles);
        }
    }
}
