using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DGraph4Net.Services;
using Google.Protobuf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DGraph4Net.Tests
{
    [Collection("DGraph4Net")]
    public class SchemaTest : ExamplesTest
    {
        [Fact]
        public async Task GetSchemaTest()
        {
            await using var dg = GetDgraphClient();

            await dg.Alter(new Operation { DropAll = true });

            var op = new Operation
            {
                Schema = @"
		            name: string @index(exact) .
		            age: int .
		            married: bool .
		            loc: geo .
		            dob: datetime .
                "
            };
            await dg.Alter(op);

            // Ask for the type of name and age.
            var resp = await dg.NewTransaction().Query("schema(pred: [name, age]) {type}");

            var json = JToken.Parse(resp.Json.ToStringUtf8());
            var expected =
                JToken.Parse(
                    @"{""schema"":[{""predicate"":""age"",""type"":""int""},{""predicate"":""name"",""type"":""string""}]}");

            Assert.True(JToken.DeepEquals(expected, json));
        }
    }
}
