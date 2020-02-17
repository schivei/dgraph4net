using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DGraph4Net.Services;
using Google.Protobuf;
using Newtonsoft.Json;
using Xunit;

namespace DGraph4Net.Tests
{
    public class SchemaTest : ExamplesTest
    {
        [Fact]
        public async Task GetSchemaTest()
        {
            using var dg = GetDgraphClient();

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

            var json = resp.Json.ToStringUtf8();

            Assert.Equal(@"{""schema"":[{""predicate"":""age"",""type"":""int""},{""predicate"":""name"",""type"":""string""}]}", json);
        }
    }
}
