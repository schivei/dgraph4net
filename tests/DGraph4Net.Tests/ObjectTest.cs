using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DGraph4Net.Services;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Assert = DGraph4Net.Tests.Assert;

namespace DGraph4Net.Tests
{
    [Collection("DGraph4Net")]
    public class ObjectTest : ExamplesTest
    {
        #region bootstrap
        private class School
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("dgraph.type")]
            public string[] DTypes { get; set; }

            [JsonProperty("since")]
            public DateTime Since { get; internal set; }
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
            public Uid Uid { get; set; }

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

            [JsonProperty("name_origin")]
            public string NameOrigin { get; set; }

            [JsonProperty("since")]
            public DateTimeOffset Since { get; set; }

            [JsonProperty("family")]
            public string Family { get; set; }

            [JsonProperty("close")]
            public bool Close { get; set; }
        }

        private class SchoolFacet
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("dgraph.type")]
            public string[] DTypes { get; set; }

            [JsonProperty("school|since")]
            public DateTime Since { get; internal set; }
        }

        private class PersonFacet
        {
            [JsonProperty("uid")]
            public Uid Uid { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("dob")]
            public DateTimeOffset Dob { get; set; }

            [JsonProperty("married")]
            public bool Married { get; set; }

            [JsonProperty("raw_bytes")]
            public byte[] Raw { get; set; }

            [JsonProperty("friend")]
            public PersonFacet[] Friends { get; set; }

            [JsonProperty("loc")]
            public Location Location { get; set; } = new Location();

            [JsonProperty("school")]
            public SchoolFacet[] Schools { get; set; }

            [JsonProperty("dgraph.type")]
            public string[] DTypes { get; set; }

            [JsonProperty("name|origin")]
            public string NameOrigin { get; set; }

            [JsonProperty("friend|since")]
            public DateTimeOffset Since { get; set; }

            [JsonProperty("friend|family")]
            public string Family { get; set; }

            [JsonProperty("friend|age")]
            public int Age { get; set; }

            [JsonProperty("friend|close")]
            public bool Close { get; set; }
        }
        #endregion

        [Fact]
        public async Task SetObjectTest()
        {
            await using var dg = GetDgraphClient();

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

            const string expected = @"{
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
             }";

            Json(expected, resp.Json.ToStringUtf8());
        }

        [Fact]
        public async Task DropAllTest()
        {
            var dg = GetDgraphClient();

            var op = new Operation { DropAll = true };

            await TaskAsync(dg.Alter, op);
        }

        [Fact]
        public async Task TxnQueryVariablesTest()
        {
            await using var dg = GetDgraphClient();

            var op = new Operation
            {
                Schema = @"
		            name: string @index(exact) .
		            type Person {
			            name: string
		            }
	            "
            };

            await dg.Alter(op);

            var p = new Person { Name = "Alice", DTypes = new[] { "Person" } };

            var mu = new Mutation { CommitNow = true };
            var pb = JsonConvert.SerializeObject(p, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            mu.SetJson = ByteString.CopyFromUtf8(pb);

            await dg.NewTransaction().Mutate(mu);

            var variables = new Dictionary<string, string>
            {
                { "$a", "Alice" }
            };

            const string q = @"
		        query Alice($a: string) {
                    me(func: eq(name, $a)) {
                        name
                        dgraph.type
                    }
                }
	        ";

            var resp = await dg.NewTransaction().QueryWithVars(q, variables);

            var me = resp.Json.ToStringUtf8();

            const string expected = @"{""me"":[{""name"":""Alice"",""dgraph.type"":[""Person""]}]}";

            Json(expected, me);
        }

        [Fact]
        public async Task TxnMutateTest()
        {
            await using var dg = GetDgraphClient();

            // While setting an object if a struct has a Uid then its properties in the
            // graph are updated else a new node is created.
            // In the example below new nodes for Alice, Bob and Charlie and school
            // are created (since they don't have a Uid).
            var p = new Person
            {
                Uid = "_:alice",
                Name = "Alice",
                Age = 26,
                Married = true,
                DTypes = new[] { "Person" },
                Location = new Location { Type = "Point", Coordinates = new[] { 1.1d, 2d }, },
                Raw = Encoding.UTF8.GetBytes("raw_bytes"),
                Friends = new[]
                {
                    new Person {Name = "Bob", Age = 24, DTypes = new[] {"Person"}},
                    new Person {Name = "Charlie", Age = 29, DTypes = new[] {"Person"}}
                },
                Schools = new[] { new School { Name = "Crown Public School", DTypes = new[] { "Institution" } } },
            };

            var op = new Operation
            {
                Schema = @"
		        name: string @index(exact) .
		        age: int .
		        married: bool .
		        friend: [uid] .
		        loc: geo .
                type: string .
		        coords: float .
		        type Person {
                    name: name
                    age: int
                    married: bool
                    friend: [Person]
                    loc: geo
                }
                type Institution {
                    name: string
                }
	        "
            };

            await dg.Alter(op);

            var mu = new Mutation { CommitNow = true, };
            var pb = JsonConvert.SerializeObject(p,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            mu.SetJson = ByteString.CopyFromUtf8(pb);
            var response = await dg.NewTransaction().Mutate(mu);

            // Assigned uids for nodes which were created would be returned in the response.Uids map.
            var puid = response.Uids["alice"];
            const string q = @"
		        query Me($id: string){
			        me(func: uid($id)) {
				        name
				        age
				        loc
				        raw_bytes
				        married
				        dgraph.type
				        friend @filter(eq(name, ""Bob"")) {
                            name
                            age
                            dgraph.type
                        }
                        school {
					        name
                            dgraph.type
                        }
                    }
		        }
            ";

            var variables = new Dictionary<string, string> { { "$id", puid } };

            var resp = await dg.NewTransaction().QueryWithVars(q, variables);

            var me = resp.Json.ToStringUtf8();

            const string expected = @"{
	        	""me"": [
	        		{
	        			""name"": ""Alice"",
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
	        }";

            Json(expected, me);
        }

        [Fact]
        public async Task TxnMutateBytesTest()
        {
            await using var dg = GetDgraphClient();

            var op = new Operation
            {
                Schema = @"
		            raw_bytes: string .
		            name: string @index(exact) .
		            type Person {
			            name: string
			            raw_bytes: string
		            }
	            "
            };

            try
            {
                await dg.Alter(op);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                throw;
            }

            var p = new Person
            {
                Name = "Alice-new",
                DTypes = new[] { "Person" },
                Raw = Encoding.UTF8.GetBytes("raw_bytes"),
            };

            var pb = JsonConvert.SerializeObject(p);

            var mu = new Mutation { CommitNow = true, SetJson = ByteString.CopyFromUtf8(pb) };
            await dg.NewTransaction().Mutate(mu);

            const string q = @"
		        {
			        q(func: eq(name, ""Alice-new"")) {
				        name
                        raw_bytes
                        dgraph.type
			        }
		        }
	        ";

            var resp = await dg.NewTransaction().Query(q);

            var me = resp.Json.ToStringUtf8();

            const string expected = @"{""q"":[{""name"":""Alice-new"",""dgraph.type"":[""Person""],""raw_bytes"":""cmF3X2J5dGVz""}]}";

            Json(expected, me);
        }

        [Fact]
        public async Task TxnQueryUnmarshalTest()
        {
            await using var dg = GetDgraphClient();

            var op = new Operation
            {
                Schema = @"
        		name: string @index(exact) .
        		age: int .
        		married: bool .
        		friend: [uid] .
        		type Person {
                    name: string
                    age: int
                    married: bool
                    friend: [uid]
                }
                type Institution {
                    name: string
                }
        	    "
            };

            await dg.Alter(op);

            var p = new Person
            {
                Uid = "_:bob",
                Name = "Bob",
                Age = 24,
                DTypes = new[] { "Person" },
            };

            var txn = dg.NewTransaction();
            var pb = JsonConvert.SerializeObject(p);

            var mu = new Mutation
            {
                CommitNow = true,
                SetJson = ByteString.CopyFromUtf8(pb)
            };
            var response = await txn.Mutate(mu);
            var bob = response.Uids["bob"];

            // While setting an object if a struct has a Uid then its properties
            // in the graph are updated else a new node is created.
            // In the example below new nodes for Alice and Charlie and school are created
            // (since they dont have a Uid).  Alice is also connected via the friend edge
            // to an existing node Bob.
            p = new Person
            {
                Uid = "_:alice",
                Name = "Alice",
                Age = 26,
                Married = true,
                DTypes = new[] { "Person" },
                Raw = Encoding.UTF8.GetBytes("raw_bytes"),
                Friends = new[]{
                    new Person{Uid = bob,Age=24},
                    new Person{Name = "Charlie", Age = 29, DTypes = new[]{ "Person"}}
                },
                Schools = new[] { new School { Name = "Crown Public School", DTypes = new[] { "Institution" } } },
            };

            txn = dg.NewTransaction();
            mu = new Mutation();
            pb = JsonConvert.SerializeObject(p);
            mu.SetJson = ByteString.CopyFromUtf8(pb);
            mu.CommitNow = true;
            response = await txn.Mutate(mu);

            // Assigned uids for nodes which were created would be returned in the response.Uids map.
            var puid = response.Uids["alice"];
            var variables = new Dictionary<string, string> { { "$id", puid } };
            const string q = @"
                query Me($id: string) {
                    me(func: uid($id)) {
                        name
                        age
                        loc
                        raw_bytes
                        married
                        dgraph.type
                        friend @filter(eq(name, ""Bob"")) {
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

            var me = resp.Json.ToStringUtf8();

            const string expected = @"{
        	    ""me"": [
        	        {
        	        	""name"": ""Alice"",
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
        	}";

            Json(expected, me);
        }

        [Fact]
        public async Task TxnQueryBestEffortTest()
        {
            await using var dg = GetDgraphClient();

            var txn = dg.NewTransaction(true, true);
            var resp = await txn.Query("{ q(func: uid(0x1)) { uid } }");

            Json(@"{""q"":[{""uid"":""0x1""}]}", resp.Json.ToStringUtf8());
        }

        [Fact]
        public async Task TxnMutateFacetsTest()
        {
            await using var dg = GetDgraphClient();

            var op = new Operation
            {
                Schema = @"
                name: string @index(exact) .
                age: int .
                married: bool .
                name_origin: string .
                since: datetime .
                family: string .
                age: int .
                close: bool .
                friend: [uid] .
                school: [uid] .
                type Person {
                    name: strng
                    age: int
                    married: bool
                    name_origin: string
                    since: datetime
                    family: string
                    close: bool
                    friend: [Person]
                    school: [Institution]
                }
                type Institution {
                    name: string
                    since: date
                }
                "
            };

            await dg.Alter(op);

            var ti = new DateTime(2009, 11, 10, 23, 0, 0, 0, DateTimeKind.Utc);

            var p = new PersonFacet
            {
                Uid = "_:alice",
                Name = "Alice",
                NameOrigin = "Indonesia",
                DTypes = new[] { "Person" },
                Friends = new[]
                {
                    new PersonFacet{Name="Bob",Since=ti,Family="yes",Age=13,Close=true,DTypes=new[]{"Person"}},
                    new PersonFacet{Name="Charlie",Family="maybe",Age=16,DTypes=new[]{"Person"}}
                },
                Schools = new[] { new SchoolFacet { Name = "Wellington School", Since = ti, DTypes = new[] { "Institution" } } }
            };

            var mu = new Mutation { SetJson = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(p)), CommitNow = true };

            var response = await dg.NewTransaction().Mutate(mu);

            var auid = response.Uids["alice"];
            var variables = new Dictionary<string, string> { { "$id", auid } };

            const string q = @"
            query Me($id: string) {
                me(func: uid($id)) {
                    name @facets
                    dgraph.type
                    friend @filter(eq(name, ""Bob"")) @facets {
                        name
                        dgraph.type
                    }
                    school @facets {
                        name
                        dgraph.type
                    }
                }
            }
            ";

            var resp = await dg.NewTransaction().QueryWithVars(q, variables);

            var me = resp.Json.ToStringUtf8();

            const string expected = @"
            {
                ""me"": [
                    {
        	            ""name|origin"": ""Indonesia"",
        	            ""name"": ""Alice"",
        	            ""dgraph.type"": [
        		            ""Person""
        	            ],
        	            ""friend"": [
        		            {
        			            ""name"": ""Bob"",
        			            ""dgraph.type"": [
        				            ""Person""
        			            ]
        		            }
        	            ],
        	            ""friend|age"": {
        		            ""0"": 13
        	            },
        	            ""friend|close"": {
        		            ""0"": true
        	            },
        	            ""friend|family"": {
        		            ""0"": ""yes""
        	            },
        	            ""friend|since"": {
        		            ""0"": ""2009-11-10T23:00:00Z""
        	            },
        	            ""school"": [
        		            {
        			            ""name"": ""Wellington School"",
        			            ""dgraph.type"": [
        				            ""Institution""
        			            ]
        		            }
        	            ],
        	            ""school|since"": {
        		            ""0"": ""2009-11-10T23:00:00Z""
        	            }
                    }
                ]
            }";

            Json(expected, me);
        }

        [Fact]
        public async Task TxnMutateVarsTest()
        {
            await using var dg = GetDgraphClient();

            // While setting an object if a struct has a Uid then its properties in the
            // graph are updated else a new node is created.
            // In the example below new nodes for Alice, Bob and Charlie and school
            // are created (since they don't have a Uid).
            var p = new Person
            {
                Uid = "_:alice",
                Name = "Alice",
                Age = 26,
                Married = true,
                DTypes = new[] { "Person" },
                Location = new Location { Type = "Point", Coordinates = new[] { 1.1d, 2d }, },
                Raw = Encoding.UTF8.GetBytes("raw_bytes"),
                Friends = new[]
                {
                    new Person {Name = "Bob", Age = 24, DTypes = new[] {"Person"}},
                    new Person {Name = "Charlie", Age = 29, DTypes = new[] {"Person"}}
                },
                Schools = new[] { new School { Name = "Crown Public School", DTypes = new[] { "Institution" } } },
            };

            var op = new Operation
            {
                Schema = @"
		        name: string @index(exact) .
		        age: int .
		        married: bool .
		        friend: [uid] .
		        loc: geo .
                type: string .
		        coords: float .
		        type Person {
                    name: name
                    age: int
                    married: bool
                    friend: [Person]
                    loc: geo
                }
                type Institution {
                    name: string
                }
	        "
            };

            await dg.Alter(op);

            var mu = new Mutation { CommitNow = true, };
            var pb = JsonConvert.SerializeObject(p,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            mu.SetJson = ByteString.CopyFromUtf8(pb);
            var response = await dg.NewTransaction().Mutate(mu);

            // Assigned uids for nodes which were created would be returned in the response.Uids map.
            var puid = response.Uids["alice"];

            var variables = new Dictionary<string, string> { { "$id", puid } };

            var q = @"
		        query Me($id: string){
			        me(func: uid($id)) {
				        friend {
                            name
                        }
                    }
		        }
            ";

            var resp = await dg.NewTransaction().QueryWithVars(q, variables);

            var me = resp.Json.ToStringUtf8();

            var expected = @"{
	        	""me"": [
	        		{
	        			""friend"": [
	        				{
	        					""name"": ""Bob""
	        				},
	        				{
	        					""name"": ""Charlie""
	        				}
	        			]
	        		}
	        	]
	        }";

            Json(expected, me, "First verifiation. Alice has 2 friends.");

            q = @"
		        {
			        charlie(func: eq(name, ""Charlie"")) {
                        uid
                    }
		        }
            ";

            resp = await dg.NewTransaction().Query(q);

            var charlie = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>[]>>(resp.Json.ToStringUtf8())["charlie"][0]["uid"];

            const string nq = "uid(a) <friend> uid(c) .";

            mu = new Mutation { CommitNow = true, DelNquads = ByteString.CopyFromUtf8(nq) };
            
            q = @"
            query Q($charlie: string, $alice: string) {
                c as var(func: uid($charlie))
                a as var(func: uid($alice))
            }";

            var req = new Request { Query = q, CommitNow = true };
            req.Vars.Add("$charlie", charlie);
            req.Vars.Add("$alice", puid);
            mu.Cond = "@if(eq(len(a), 1) AND eq(len(c), 1))";
            req.Mutations.Add(mu);

            var tx = dg.NewTransaction();
            await tx.Do(req);

            q = @"
		        query Me($id: string){
			        me(func: uid($id)) {
				        friend {
                            name
                        }
                    }
		        }
            ";

            resp = await dg.NewTransaction().QueryWithVars(q, variables);

            me = resp.Json.ToStringUtf8();

            expected = @"{
	        	""me"": [
	        		{
	        			""friend"": [
	        				{
	        					""name"": ""Bob""
	        				}
	        			]
	        		}
	        	]
	        }";

            Json(expected, me, "Second verification. Now Alice has only one friend.");
        }

        //        func ExampleTxn_Mutate_list() {
        //	        dg, cancel := getDgraphClient()
        //	        defer cancel() // This example shows example for SetObject for predicates with list type.
        //	        type Person struct {
        //		        Uid         string   `json:"uid"`
        //		        Address     []string `json:"address"`
        //		        PhoneNumber []int64  `json:"phone_number"`
        //		        DType       []string `json:"dgraph.type,omitempty"`
        //	        }

        //	        p := Person{
        //		        Uid:         "_:person",
        //		        Address:     []string{"Redfern", "Riley Street"},
        //		        PhoneNumber: []int64{9876, 123},
        //		        DType:       []string{"Person"},
        //	        }

        //	        op := &api.Operation{}
        //	        op.Schema = `
        //		        address: [string] .
        //		        phone_number: [int] .
        //		        type Person {
        //			        Address
        //			        phone_number
        //		          }
        //	        `

        //	        ctx := context.Background()
        //	        err := dg.Alter(ctx, op)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        mu := &api.Mutation{}
        //	        pb, err := json.Marshal(p)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        mu.SetJson = pb
        //	        mu.CommitNow = true
        //	        response, err := dg.NewTxn().Mutate(ctx, mu)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        variables := map[string]string{"$id": response.Uids["person"]}
        //	        const q = `
        //		        query Me($id: string){
        //			        me(func: uid($id)) {
        //				        address
        //				        phone_number
        //				        dgraph.type
        //			        }
        //		        }
        //	        `

        //	        resp, err := dg.NewTxn().QueryWithVars(ctx, q, variables)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        type Root struct {
        //		        Me []Person `json:"me"`
        //	        }

        //	        var r Root
        //	        err = json.Unmarshal(resp.Json, &r)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        // List items aren't guaranteed to be in the same order.
        //	        fmt.Println(string(resp.Json))
        //	        // {"me":[{"address":["Riley Street","Redfern"],"phone_number":[9876,123]}]}

        //        }

        //        func ExampleDeleteEdges() {
        //	        dg, cancel := getDgraphClient()
        //	        defer cancel()
        //	        op := &api.Operation{}
        //	        op.Schema = `
        //		        age: int .
        //		        married: bool .
        //		        name: string @lang .
        //		        location: string .
        //		        Friends: [uid] .
        //		        type Person {
        //			        name
        //			        age
        //			        married
        //			        Friends
        //		        }
        //		        type Institution {
        //			        name
        //		        }
        //		        type Institution {
        //			        name: string
        //		        }
        //	        `

        //	        ctx := context.Background()
        //	        err := dg.Alter(ctx, op)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        type School struct {
        //		        Uid   string   `json:"uid"`
        //		        Name  string   `json:"name@en,omitempty"`
        //		        DType []string `json:"dgraph.type,omitempty"`
        //	        }

        //	        type Person struct {
        //		        Uid      string    `json:"uid,omitempty"`
        //		        Name     string    `json:"name,omitempty"`
        //		        Age      int       `json:"age,omitempty"`
        //		        Married  bool      `json:"married,omitempty"`
        //		        Friends  []Person  `json:"friends,omitempty"`
        //		        Location string    `json:"location,omitempty"`
        //		        Schools  []*School `json:"schools,omitempty"`
        //		        DType    []string  `json:"dgraph.type,omitempty"`
        //	        }

        //	        // Lets add some data first.
        //	        p := Person{
        //		        Uid:      "_:alice",
        //		        Name:     "Alice",
        //		        Age:      26,
        //		        Married:  true,
        //		        DType:    []string{"Person"},
        //		        Location: "Riley Street",
        //		        Friends: []Person{{
        //			        Name:  "Bob",
        //			        Age:   24,
        //			        DType: []string{"Person"},
        //		        }, {
        //			        Name:  "Charlie",
        //			        Age:   29,
        //			        DType: []string{"Person"},
        //		        }},
        //		        Schools: []*School{&School{
        //			        Name:  "Crown Public School",
        //			        DType: []string{"Institution"},
        //		        }},
        //	        }

        //	        mu := &api.Mutation{}
        //	        pb, err := json.Marshal(p)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        mu.SetJson = pb
        //	        mu.CommitNow = true
        //	        response, err := dg.NewTxn().Mutate(ctx, mu)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        alice := response.Uids["alice"]

        //	        variables := make(map[string]string)
        //	        variables["$alice"] = alice
        //	        const q = `
        //		        query Me($alice: string){
        //			        me(func: uid($alice)) {
        //				        name
        //				        age
        //				        location
        //				        married
        //				        dgraph.type
        //				        friends {
        //					        name
        //					        age
        //					        dgraph.type
        //				        }
        //				        schools {
        //					        name@en
        //					        dgraph.type
        //				        }
        //			        }
        //		        }
        //	        `

        //	        resp, err := dg.NewTxn().QueryWithVars(ctx, q, variables)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        // Now lets delete the friend and location edge from Alice
        //	        mu = &api.Mutation{}
        //	        dgo.DeleteEdges(mu, alice, "friends", "location")

        //	        mu.CommitNow = true
        //	        _, err = dg.NewTxn().Mutate(ctx, mu)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        resp, err = dg.NewTxn().QueryWithVars(ctx, q, variables)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        type Root struct {
        //		        Me []Person `json:"me"`
        //	        }

        //	        var r Root
        //	        err = json.Unmarshal(resp.Json, &r)
        //	        out, _ := json.MarshalIndent(r.Me, "", "\t")
        //	        fmt.Printf("%s\n", out)
        //	        // Output: [
        //	        // 	{
        //	        // 		"name": "Alice",
        //	        // 		"age": 26,
        //	        // 		"married": true,
        //	        // 		"schools": [
        //	        // 			{
        //	        // 				"uid": "",
        //	        // 				"name@en": "Crown Public School",
        //	        // 				"dgraph.type": [
        //	        // 					"Institution"
        //	        // 				]
        //	        // 			}
        //	        // 		],
        //	        // 		"dgraph.type": [
        //	        // 			"Person"
        //	        // 		]
        //	        // 	}
        //	        // ]
        //        }

        //        func ExampleTxn_Mutate_deleteNode() {
        //	        dg, cancel := getDgraphClient()
        //	        defer cancel()
        //	        // In this test we check S * * deletion.
        //	        type Person struct {
        //		        Uid     string    `json:"uid,omitempty"`
        //		        Name    string    `json:"name,omitempty"`
        //		        Age     int       `json:"age,omitempty"`
        //		        Married bool      `json:"married,omitempty"`
        //		        Friends []*Person `json:"friends,omitempty"`
        //		        DType   []string  `json:"dgraph.type,omitempty"`
        //	        }

        //	        p := Person{
        //		        Uid:     "_:alice",
        //		        Name:    "Alice",
        //		        Age:     26,
        //		        Married: true,
        //		        DType:   []string{"Person"},
        //		        Friends: []*Person{&Person{
        //			        Uid:   "_:bob",
        //			        Name:  "Bob",
        //			        Age:   24,
        //			        DType: []string{"Person"},
        //		        }, &Person{
        //			        Uid:   "_:charlie",
        //			        Name:  "Charlie",
        //			        Age:   29,
        //			        DType: []string{"Person"},
        //		        }},
        //	        }

        //	        op := &api.Operation{}
        //	        op.Schema = `
        //		        age: int .
        //		        married: bool .
        //		        friends: [uid] .
        //		        type Person {
        //			        name: string
        //			        age: int
        //			        married: bool
        //			        friends: [Person]
        //		        }
        //	        `

        //	        ctx := context.Background()
        //	        err := dg.Alter(ctx, op)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        mu := &api.Mutation{
        //		        CommitNow: true,
        //	        }
        //	        pb, err := json.Marshal(p)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        mu.SetJson = pb
        //	        response, err := dg.NewTxn().Mutate(ctx, mu)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        alice := response.Uids["alice"]
        //	        bob := response.Uids["bob"]
        //	        charlie := response.Uids["charlie"]

        //	        variables := make(map[string]string)
        //	        variables["$alice"] = alice
        //	        variables["$bob"] = bob
        //	        variables["$charlie"] = charlie
        //	        const q = `
        //		        query Me($alice: string, $bob: string, $charlie: string){
        //			        me(func: uid($alice)) {
        //				        name
        //				        age
        //				        married
        //				        dgraph.type
        //				        friends {
        //					        uid
        //					        name
        //					        age
        //					        dgraph.type
        //				        }
        //			        }
        //			        me2(func: uid($bob)) {
        //				        name
        //				        age
        //				        dgraph.type
        //			        }
        //			        me3(func: uid($charlie)) {
        //				        name
        //				        age
        //				        dgraph.type
        //			        }
        //		        }
        //	        `

        //	        resp, err := dg.NewTxn().QueryWithVars(ctx, q, variables)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        type Root struct {
        //		        Me  []Person `json:"me"`
        //		        Me2 []Person `json:"me2"`
        //		        Me3 []Person `json:"me3"`
        //	        }

        //	        var r Root
        //	        err = json.Unmarshal(resp.Json, &r)

        //	        // Now lets try to delete Alice. This won't delete Bob and Charlie
        //	        // but just remove the connection between Alice and them.

        //	        // The JSON for deleting a node should be of the form {"uid": "0x123"}.
        //	        // If you wanted to delete multiple nodes you could supply an array of objects
        //	        // like [{"uid": "0x321"}, {"uid": "0x123"}] to DeleteJson.

        //	        d := map[string]string{"uid": alice}
        //	        pb, err = json.Marshal(d)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        mu = &api.Mutation{
        //		        CommitNow:  true,
        //		        DeleteJson: pb,
        //	        }

        //	        _, err = dg.NewTxn().Mutate(ctx, mu)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        resp, err = dg.NewTxn().QueryWithVars(ctx, q, variables)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        err = json.Unmarshal(resp.Json, &r)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        out, _ := json.MarshalIndent(r, "", "\t")
        //	        fmt.Printf("%s\n", out)
        //	        // Output: {
        //	        // 	"me": [],
        //	        // 	"me2": [
        //	        // 		{
        //	        // 			"name": "Bob",
        //	        // 			"age": 24,
        //	        // 			"dgraph.type": [
        //	        // 				"Person"
        //	        // 			]
        //	        // 		}
        //	        // 	],
        //	        // 	"me3": [
        //	        // 		{
        //	        // 			"name": "Charlie",
        //	        // 			"age": 29,
        //	        // 			"dgraph.type": [
        //	        // 				"Person"
        //	        // 			]
        //	        // 		}
        //	        // 	]
        //	        // }
        //        }

        //        func ExampleTxn_Mutate_deletePredicate() {
        //	        dg, cancel := getDgraphClient()
        //	        defer cancel()
        //	        type Person struct {
        //		        Uid     string   `json:"uid,omitempty"`
        //		        Name    string   `json:"name,omitempty"`
        //		        Age     int      `json:"age,omitempty"`
        //		        Married bool     `json:"married,omitempty"`
        //		        Friends []Person `json:"friends,omitempty"`
        //		        DType   []string `json:"dgraph.type,omitempty"`
        //	        }

        //	        p := Person{
        //		        Uid:     "_:alice",
        //		        Name:    "Alice",
        //		        Age:     26,
        //		        Married: true,
        //		        DType:   []string{"Person"},
        //		        Friends: []Person{Person{
        //			        Name:  "Bob",
        //			        Age:   24,
        //			        DType: []string{"Person"},
        //		        }, Person{
        //			        Name:  "Charlie",
        //			        Age:   29,
        //			        DType: []string{"Person"},
        //		        }},
        //	        }

        //	        op := &api.Operation{}
        //	        op.Schema = `
        //		        name: string .
        //		        age: int .
        //		        married: bool .
        //		        friends: [uid] .
        //		        type Person {
        //			        name: string
        //			        age: int
        //			        married: bool
        //			        friends: [Person]
        //		        }
        //	        `

        //	        ctx := context.Background()
        //	        err := dg.Alter(ctx, op)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        mu := &api.Mutation{
        //		        CommitNow: true,
        //	        }
        //	        pb, err := json.Marshal(p)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        mu.SetJson = pb
        //	        response, err := dg.NewTxn().Mutate(ctx, mu)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        alice := response.Uids["alice"]

        //	        variables := make(map[string]string)
        //	        variables["$id"] = alice
        //	        const q = `
        //		        query Me($id: string){
        //			        me(func: uid($id)) {
        //				        name
        //				        age
        //				        married
        //				        dgraph.type
        //				        friends {
        //					        uid
        //					        name
        //					        age
        //					        dgraph.type
        //				        }
        //			        }
        //		        }
        //	        `

        //	        resp, err := dg.NewTxn().QueryWithVars(ctx, q, variables)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        type Root struct {
        //		        Me []Person `json:"me"`
        //	        }
        //	        var r Root
        //	        err = json.Unmarshal(resp.Json, &r)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        op = &api.Operation{DropAttr: "friends"}
        //	        err = dg.Alter(ctx, op)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        op = &api.Operation{DropAttr: "married"}
        //	        err = dg.Alter(ctx, op)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        // Also lets run the query again to verify that predicate data was deleted.
        //	        resp, err = dg.NewTxn().QueryWithVars(ctx, q, variables)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        r = Root{}
        //	        err = json.Unmarshal(resp.Json, &r)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        // Alice should have no friends and only two attributes now.
        //	        fmt.Printf("%+v\n", r)
        //	        // Output: {Me:[{Uid: Name:Alice Age:26 Married:false Friends:[] DType:[Person]}]}
        //        }

        //        func ExampleTxn_Discard() {
        //	        dg, cancel := getDgraphClient()
        //	        defer cancel()

        //	        ctx, toCancel := context.WithTimeout(context.Background(), 30*time.Second)
        //	        defer toCancel()
        //	        err := dg.Alter(ctx, &api.Operation{
        //		        DropAll: true,
        //	        })
        //	        if err != nil {
        //		        log.Fatal("The drop all operation should have succeeded")
        //	        }

        //	        err = dg.Alter(ctx, &api.Operation{
        //		        Schema: `name: string @index(exact) .`,
        //	        })
        //	        if err != nil {
        //		        log.Fatal("The alter should have succeeded")
        //	        }

        //	        txn := dg.NewTxn()
        //	        _, err = txn.Mutate(ctx, &api.Mutation{
        //		        SetNquads: []byte(`_:a <name> "Alice" .`),
        //	        })
        //	        if err != nil {
        //		        log.Fatal("The mutation should have succeeded")
        //	        }
        //	        txn.Discard(ctx)

        //	        // now query the cluster and make sure that the data has made no effect
        //	        queryTxn := dg.NewReadOnlyTxn()
        //	        query := `
        //		        {
        //			        q (func: eq(name, "Alice")) {
        //				        name
        //				        dgraph.type
        //			        }
        //		        }
        //	        `
        //	        resp, err := queryTxn.Query(ctx, query)
        //	        if err != nil {
        //		        log.Fatal("The query should have succeeded")
        //	        }

        //	        fmt.Printf(string(resp.Json))
        //	        // Output: {"q":[]}
        //        }

        //        func ExampleTxn_Mutate_upsert() {
        //	        dg, cancel := getDgraphClient()
        //	        defer cancel()

        //	        ctx, toCancel := context.WithTimeout(context.Background(), 30*time.Second)
        //	        defer toCancel()

        //	        // Warn: Cleaning up the database
        //	        if err := dg.Alter(ctx, &api.Operation{DropAll: true}); err != nil {
        //		        log.Fatal("The drop all operation should have succeeded")
        //	        }

        //	        op := &api.Operation{}
        //	        op.Schema = `
        //		        name: string .
        //		        email: string @index(exact) .`
        //	        if err := dg.Alter(ctx, op); err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        m1 := `
        //		        _:n1 <name> "user" .
        //		        _:n1 <email> "user@dgraphO.io" .
        //	        `
        //	        mu := &api.Mutation{
        //		        SetNquads: []byte(m1),
        //		        CommitNow: true,
        //	        }
        //	        if _, err := dg.NewTxn().Mutate(ctx, mu); err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        req := &api.Request{CommitNow: true}
        //	        req.Query = `
        //		        query {
        //  			        me(func: eq(email, "user@dgraphO.io")) {
        //	    		        v as uid
        //  			        }
        //		        }
        //	        `
        //	        m2 := `uid(v) <email> "user@dgraph.io" .`
        //	        mu.SetNquads = []byte(m2)
        //	        req.Mutations = []*api.Mutation{mu}

        //	        // Update email only if matching uid found.
        //	        if _, err := dg.NewTxn().Do(ctx, req); err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        query := `
        //		        {
        //			        me(func: eq(email, "user@dgraph.io")) {
        //				        name
        //				        email
        //				        dgraph.type
        //			        }
        //		        }
        //	        `
        //	        resp, err := dg.NewTxn().Query(ctx, query)
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        // resp.Json contains the updated value.
        //	        fmt.Println(string(resp.Json))
        //	        // Output: {"me":[{"name":"user","email":"user@dgraph.io"}]}
        //        }

        //        func ExampleTxn_Mutate_upsertJSON() {
        //	        dg, cancel := getDgraphClient()
        //	        defer cancel()

        //	        // Warn: Cleaning up the database
        //	        ctx := context.Background()
        //	        if err := dg.Alter(ctx, &api.Operation{DropAll: true}); err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        type Person struct {
        //		        Uid     string   `json:"uid,omitempty"`
        //		        Name    string   `json:"name,omitempty"`
        //		        Age     int      `json:"age,omitempty"`
        //		        Email   string   `json:"email,omitempty"`
        //		        Friends []Person `json:"friends,omitempty"`
        //		        DType   []string `json:"dgraph.type,omitempty"`
        //	        }

        //	        op := &api.Operation{Schema: `email: string @index(exact) @upsert .`}
        //	        if err := dg.Alter(context.Background(), op); err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        // Create and query the user using Upsert block
        //	        req := &api.Request{CommitNow: true}
        //	        req.Query = `
        //		        {
        //			        me(func: eq(email, "user@dgraph.io")) {
        //				        ...fragmentA
        //			        }
        //		        }
        //		        fragment fragmentA {
        //			        v as uid
        //		        }
        //	        `
        //	        pb, err := json.Marshal(Person{Uid: "uid(v)", Name: "Wrong", Email: "user@dgraph.io"})
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }
        //	        mu := &api.Mutation{SetJson: pb}
        //	        req.Mutations = []*api.Mutation{mu}
        //	        if _, err := dg.NewTxn().Do(ctx, req); err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        // Fix the name and add age
        //	        pb, err = json.Marshal(Person{Uid: "uid(v)", Name: "user", Age: 35})
        //	        if err != nil {
        //		        log.Fatal(err)
        //	        }
        //	        mu.SetJson = pb
        //	        req.Mutations = []*api.Mutation{mu}
        //	        if _, err := dg.NewTxn().Do(ctx, req); err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        q := `
        //  		        {
        //			        Me(func: has(email)) {
        //				        age
        //				        name
        //				        email
        //				        dgraph.type
        //			        }
        //		        }
        //	        `
        //	        resp, err := dg.NewReadOnlyTxn().Query(ctx, q)
        //	        if err != nil {
        //		        log.Fatal("The query should have succeeded")
        //	        }

        //	        type Root struct {
        //		        Me []Person `json:"me"`
        //	        }
        //	        var r Root
        //	        if err := json.Unmarshal(resp.Json, &r); err != nil {
        //		        log.Fatal(err)
        //	        }
        //	        fmt.Println(string(resp.Json))

        //	        // Delete the user now
        //	        mu.SetJson = nil
        //	        dgo.DeleteEdges(mu, "uid(v)", "age", "name", "email")
        //	        req.Mutations = []*api.Mutation{mu}
        //	        if _, err := dg.NewTxn().Do(ctx, req); err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        resp, err = dg.NewReadOnlyTxn().Query(ctx, q)
        //	        if err != nil {
        //		        log.Fatal("The query should have succeeded")
        //	        }
        //	        if err := json.Unmarshal(resp.Json, &r); err != nil {
        //		        log.Fatal(err)
        //	        }

        //	        fmt.Println(string(resp.Json))
        //	        // Output: {"Me":[{"age":35,"name":"user","email":"user@dgraph.io"}]}
        //	        // {"Me":[]}
        //        }
    }
}
