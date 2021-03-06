using System.Threading.Tasks;

using Api;

using Xunit;

namespace Dgraph4Net.Tests
{
    [Collection("Dgraph4Net")]
    public class SchemaTest : ExamplesTest
    {
        [Fact]
        public async Task GetSchemaTest()
        {
            var dg = GetDgraphClient();

            try
            {
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

                const string expected = @"{""schema"":[{""predicate"":""age"",""type"":""int""},{""predicate"":""name"",""type"":""string""}]}";

                Json(expected, resp.Json.ToStringUtf8());
            }
            finally
            {
                await ClearDB();
            }
        }
    }
}
