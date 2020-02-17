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
    public class ObjectTest : ExamplesTest
    {
        #region bootstrap
        private class School
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("dgraph.type")]
            public string[] DTypes { get; set; }
        }

        private class Location
        {
            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("coordinates")]
            public double[] Coordinates { get; set; }
        }

        private class Person
        {
            [JsonProperty("uid")]
            public string Uid { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("age")]
            public int Age { get; set; }

            [JsonProperty("dob")]
            public DateTimeOffset Dob { get; set; }

            [JsonProperty("married")]
            public bool Married { get; set; }

            [JsonProperty("raw_bytes")]
            public byte[] Raw { get; set; }

            [JsonProperty("friend")]
            public Person[] Friends { get; set; }

            [JsonProperty("loc")]
            public Location Location { get; set; } = new Location();

            [JsonProperty("school")]
            public School[] Schools { get; set; }

            [JsonProperty("dgraph.type")]
            public string[] DTypes { get; set; }
        }
        #endregion

        [Fact]
        public async Task SetObjectTest()
        {
            using var dg = GetDgraphClient();

            await dg.Alter(new Operation { DropAll = true });

            var dob = new DateTimeOffset(new DateTime(1980, 01, 01, 23, 0, 0, 0, DateTimeKind.Utc));
            // While setting an object if a struct has a Uid then its properties in the graph are updated
            // else a new node is created.
            // In the example below new nodes for Alice, Bob and Charlie and school are created (since they
            // don't have a Uid).
            var p = new Person
            {
                Uid = "_:alice",
                Name = "Alice",
                Age = 26,
                Married = true,
                DTypes = new[] { "Person" },
                Location = new Location
                {
                    Type = "Point",
                    Coordinates = new[] { 1.1d, 2d },
                },
                Dob = dob,
                Raw = Encoding.UTF8.GetBytes("raw_bytes"),
                Friends = new[]{new Person {
                    Name =  "Bob",
                    Age =   24,
                    DTypes = new []{"Person"},
                }, new Person{
                    Name =  "Charlie",
                    Age =   29,
                    DTypes = new []{"Person"},
                }},
                Schools = new[]{ new School{
                    Name =  "Crown Public School",
                    DTypes = new []{"Institution"},
                }},
            };

            var op = new Operation
            {
                Schema = @"
                    name: string @index(exact) .
		            age: int .
		            married: bool .
		            loc: geo .
		            dob: datetime .
		            Friend: [uid] .
		            type: string .
		            coords: float .
		            type Person {
			            name: string
			            age: int
			            married: bool
			            Friend: [Person]
			            loc: Loc
		            }
		            type Institution {
			            name: string
		            }
		            type Loc {
			            type: string
			            coords: float
		            }
                "
            };

            await dg.Alter(op);

            var mu = new Mutation
            {
                CommitNow = true,
            };

            var json = JsonConvert.SerializeObject(p, new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            });

            mu.SetJson = ByteString.CopyFromUtf8(json);

            var response = await dg.NewTransaction().Mutate(mu);

            // Assigned uids for nodes which were created would be returned in the response.Uids map.
            var variables = new Dictionary<string, string>() { { "$id1", response.Uids["alice"] } };
            const string q = @"query Me($id1: string){
		        me(func: uid($id1)) {
			        name
			        dob
			        age
			        loc
			        raw_bytes
			        married
			        dgraph.type
			        friend @filter(eq(name, ""Bob"")){
				        name
				        age
				        dgraph.type
			        }
			        school {
				        name
				        dgraph.type
			        }
		        }
	        }";

            var resp = await dg.NewTransaction().QueryWithVars(q, variables);

            var me = JToken.Parse(resp.Json.ToStringUtf8());

            var expected = JToken.Parse(@"{
             	""me"": [
             		{
             			""name"": ""Alice"",
             			""dob"": ""1980-01-01T23:00:00Z"",
             			""age"": 26,
             			""married"": true,
             			""raw_bytes"": ""cmF3X2J5dGVz"",
             			""friend"": [
             				{
             					""name"": ""Bob"",
             					""age"": 24,
             					""dgraph.type"": [
             						""Person""
             					]
             				}
             			],
             			""loc"": {
             				""type"": ""Point"",
             				""coordinates"": [
             					1.1,
             					2
             				]
             			},
             			""school"": [
             				{
             					""name"": ""Crown Public School"",
             					""dgraph.type"": [
             						""Institution""
             					]
             				}
             			],
             			""dgraph.type"": [
             				""Person""
             			]
             		}
             	]
             }");

            Assert.True(JToken.DeepEquals(expected, me));
        }
    }
}
