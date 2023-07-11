using System.Threading.Tasks;

using Api;

using Xunit;

namespace Dgraph4Net.Tests;

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
                        sct.name: string @index(exact) .
                        sct.age: int .
                        sct.married: bool .
                        sct.loc: geo .
                        sct.dob: datetime .
                    "
            };
            await dg.Alter(op);

            // Ask for the type of name and age.
            var resp = await dg.NewTransaction().Query("schema(pred: [sct.name, sct.age]) {type}");

            const string expected = @"{""schema"":[{""predicate"":""sct.age"",""type"":""int""},{""predicate"":""sct.name"",""type"":""string""}]}";

            var actual = resp.Json.ToStringUtf8();

            Equal(expected, actual);
        }
        finally
        {
            await ClearDB();
        }
    }
}
