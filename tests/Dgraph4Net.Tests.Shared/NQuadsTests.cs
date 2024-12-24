using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Dgraph4Net.ActiveRecords;

using ICM = Dgraph4Net.ActiveRecords.InternalClassMapping;

namespace Dgraph4Net.Tests;

[Collection("Dgraph4Net")]
public class NQuadsTests : ExamplesTest
{
    [Fact]
    public async Task NQuadsComparerTest()
    {
        try
        {
            Uid id = "0x1";
            Uid refs = "_:0";

            var test = new Testing
            {
                Name = "test",
                Test = new(),
                Ways = ["1", "2"]
            };

            test.Uid.Replace(id);
            test.Test.Uid.Replace(refs);

            test.Ways[1].SetFacet("facet", 123);

            string[] expected = [
                """<0x1> <ways> "1" .""",
                """<0x1> <ways> "2" (facet=123) .""",
                """<0x1> <dgraph.type> "Testing" .""",
                """_:0 <dgraph.type> "Testing" .""",
                "<0x1> <parent> _:0 .",
                """<0x1> <name> "test" ."""
            ];

            var (bs, _) = test.ToNQuads();
            var me = bs.ToStringUtf8().Replace("\r\n", "\n").Split("\n");

            Equivalent(expected, me);
        }
        finally
        {
            await ClearDb();
        }
    }

    [Fact]
    public async Task NQuadsMutationTest()
    {
        var client = GetDgraphClient();

        try
        {
            var test = new Testing
            {
                Name = "test",
                Test = new(),
                Ways = ["1", "2"]
            };

            test.Ways[1].SetFacet("facet", 123);

            var migrations = ICM.Migrations;

            await ICM.EnsureAsync(client);

            foreach (var mig in migrations)
            {
                mig.SetClient(client);
                await mig.MigrateUp();
            }

            await using var txn = client.NewTransaction(false, false, null, true);

            await txn.Save(test);

            // to ensure the complete data propagation during tests
            await Task.Delay(1000);

            False("_:0" == test.Uid, "Uid not propagated");

            await using var qtx = client.NewTransaction(true, true);

            var query = $$"""
query {
    testing(func: uid({{test.Uid}})) {
        uid
        name
        parent @facets {
            uid
            name
            ways @facets
            dgraph.type
        }
        ways @facets
        dgraph.type
    }
}
""";

            List<Testing> res;

            var tries = 0;
            while ((res = await qtx.Query<Testing>("testing", query)).Count == 0 && tries++ < 3)
                await Task.Delay(1000);

            Equal(1, res.Count);

            var testing = res[0];

            Equivalent(test, testing);
        }
        finally
        {
            await ClearDb();
        }
    }
}
