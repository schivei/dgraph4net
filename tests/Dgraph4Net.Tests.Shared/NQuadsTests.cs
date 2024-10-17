using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dgraph4Net.ActiveRecords;

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
                @"<0x1> <ways> ""1"" .",
                @"<0x1> <ways> ""2"" (facet=123) .",
                @"<0x1> <dgraph.type> ""Testing"" .",
                @"_:0 <dgraph.type> ""Testing"" .",
                @"<0x1> <parent> _:0 .",
                @"<0x1> <name> ""test"" ."
            ];

            var (bs, del) = test.ToNQuads();
            var me = bs.ToStringUtf8().Replace("\r\n", "\n").Split("\n");

            Equivalent(expected, me);
        }
        finally
        {
            await ClearDB();
        }
    }

    [Fact]
    public async Task NQuadsMutationTest()
    {
        var client = GetDgraphClient();

        var expressed = ExpressedFilterFunctions.Parse(func => func.Has<Testing>(x => x.Name) && (func.Has<Person>(p => p.Age) || func.Eq<Person, string>(x => x.Family, "teste")));

        var expectedExpression = $"has(name) and (has(age) or eq(family, {expressed.Variables["family"].Name}))";

        var vars = expressed.Variables.ToQueryString();

        var expectedVars = $"{expressed.Variables["family"].Name}: string";

        var ex = expressed.ToString();

        Equal(expectedExpression, ex);

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

            test.Ways[1].SetFacet("facet", 123);

            ClassMapping.ClassMappings.TryGetValue(typeof(Testing), out var map);

            var migrations = InternalClassMapping.Migrations;

            await InternalClassMapping.EnsureAsync(client);

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

            var query = @$"query {{
    testing(func: uid({test.Uid})) {{
        uid
        name
        parent @facets {{
            uid
            name
            ways @facets
            dgraph.type
        }}
        ways @facets
        dgraph.type
    }}
}}";

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
            await ClearDB();
        }
    }
}
