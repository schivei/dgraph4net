using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Api;

using Google.Protobuf;

using System.Text.Json.Serialization;

using Xunit;
using System.Text.Json;
using Dgraph4Net.Core.GeoLocation;
using System.Linq;
using System.IO;

namespace Dgraph4Net.Tests;

[Collection("Dgraph4Net")]
public class ObjectTest : ExamplesTest
{
    #region bootstrap
    private class School
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("dgraph.type")]
        public string[] DTypes { get; set; }

        [JsonPropertyName("since")]
        public DateTime Since { get; set; }

        [JsonPropertyName("uid")]
        public Uid Uid { get; set; } = Uid.NewUid();
    }

    private class Person
    {
        [JsonPropertyName("uid")]
        public Uid Uid { get; set; } = Uid.NewUid();

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("age")]
        public int Age { get; set; }

        [JsonPropertyName("dob")]
        public DateTimeOffset Dob { get; set; }

        [JsonPropertyName("married")]
        public bool Married { get; set; }

        [JsonPropertyName("raw_bytes")]
        public byte[] Raw { get; set; }

        [JsonPropertyName("friend")]
        public Person[] Friends { get; set; }

        [JsonPropertyName("loc")]
        public string Location { get; set; }

        [JsonPropertyName("school")]
        public School[] Schools { get; set; }

        [JsonPropertyName("dgraph.type")]
        public string[] DTypes { get; set; }

        [JsonPropertyName("name_origin")]
        public string NameOrigin { get; set; }

        [JsonPropertyName("since")]
        public DateTimeOffset Since { get; set; }

        [JsonPropertyName("family")]
        public string Family { get; set; }

        [JsonPropertyName("close")]
        public bool Close { get; set; }
    }

    private class SchoolFacet
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("dgraph.type")]
        public string[] DTypes { get; set; }

        [JsonPropertyName("school|since")]
        public DateTime? Since { get; internal set; }
    }

    private class PersonFacet
    {
        [JsonPropertyName("uid")]
        public Uid Uid { get; set; } = Uid.NewUid();

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("dob")]
        public DateTimeOffset Dob { get; set; }

        [JsonPropertyName("married")]
        public bool Married { get; set; }

        [JsonPropertyName("raw_bytes")]
        public byte[] Raw { get; set; }

        [JsonPropertyName("friend")]
        public PersonFacet[] Friends { get; set; }

        [JsonPropertyName("loc")]
        public GeoObject Location { get; set; }

        [JsonPropertyName("school")]
        public SchoolFacet[] Schools { get; set; }

        [JsonPropertyName("dgraph.type")]
        public string[] DTypes { get; set; }

        [JsonPropertyName("name|origin")]
        public string NameOrigin { get; set; }

        [JsonPropertyName("friend|since")]
        public DateTimeOffset? Since { get; set; }

        [JsonPropertyName("family")]
        public string Family { get; set; }

        [JsonPropertyName("age")]
        public int? Age { get; set; }

        [JsonPropertyName("friend|close")]
        public bool Close { get; set; }
    }
    #endregion

    [Fact]
    public async Task SetObjectTest()
    {
        var dg = GetDgraphClient();

        try
        {
            //await dg.Alter(new Operation { DropAll = true });

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
                Location = JsonSerializer.Serialize(new Point(new Position(1.1, 2))),
                Dob = dob,
                Raw = Encoding.UTF8.GetBytes("raw_bytes"),
                Friends = new[]{new Person {
                    Uid = "_:bob",
                    Name =  "Bob",
                    Age =   24,
                    DTypes = new []{"Person"},
                }, new Person{
                    Uid = "_:charlie",
                    Name =  "Charlie",
                    Age =   29,
                    DTypes = new []{"Person"},
                }},
                Schools = new[]{ new School{
                    Uid = "_:school",
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
                        loc: geo
                    }
                    type Institution {
                        name: string
                    }
                "
            };

            await dg.Alter(op);

            var mu = new Mutation
            {
                CommitNow = true,
            };

            var json = JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = false });

            mu.SetJson = ByteString.CopyFromUtf8(json);

            var response = await dg.NewTransaction().Mutate(mu);

            // Assigned uids for nodes which were created would be returned in the response.Uids map.
            var variables = new Dictionary<string, string>() { { "$id1", "Alice" } };
            const string q = @"query Me($id1: string){
                me(func: eq(name, $id1)) {
                    name
                    dob
                    age
                    married
                    raw_bytes
                    friend @filter(eq(name, ""Bob"")){
                        name
                        age
                        dgraph.type
                    }
                    loc
                    school {
                        name
                        dgraph.type
                    }
                    dgraph.type
                }
            }";

            var resp = await dg.NewTransaction().QueryWithVars(q, variables);

            var expected = JsonSerializer.Serialize(
                JsonSerializer.Deserialize<object>(@"{
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
             }"), new JsonSerializerOptions { WriteIndented = false }
            );

            var actual = resp.Json.ToStringUtf8().Trim();

            Equivalent(expected, actual);
        }
        finally
        {
            await ClearDB();
        }
    }

    [Fact(DisplayName = "DropAll: Only deleting user types and predicates instead of all objects.")]
    public async Task DropAllTest()
    {
        try
        {
            var dg = GetDgraphClient();

            var op = new Operation
            {
                DropAll = true,
                DropOp = Operation.Types.DropOp.All,
                RunInBackground = false
            };

            await TaskAsync(dg.Alter, op);
        }
        finally
        {
            await ClearDB();
        }
    }

    [Fact]
    public async Task TxnQueryVariablesTest()
    {
        var dg = GetDgraphClient();

        try
        {
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
            var pb = JsonSerializer.Serialize(p);

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

            Equal(expected, me);
        }
        finally
        {
            await ClearDB();
        }
    }

    [Fact]
    public async Task TxnMutateTest()
    {
        var dg = GetDgraphClient();

        try
        {
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
                Location = JsonSerializer.Serialize(new Point(new Position(1.1, 2))),
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
            var pb = JsonSerializer.Serialize(p);

            mu.SetJson = ByteString.CopyFromUtf8(pb);
            var response = await dg.NewTransaction().Mutate(mu);

            // Assigned uids for nodes which were created would be returned in the response.Uids map.
            var puid = "Alice";
            const string q = @"
                    query Me($id: string){
                        me(func: eq(name, $id)) {
                            name
                            age
                            married
                            raw_bytes
                            friend @filter(eq(name, ""Bob"")) {
                                name
                                age
                                dgraph.type
                            }
                            loc
                            school {
                                name
                                dgraph.type
                            }
                            dgraph.type
                        }
                    }
                ";

            var variables = new Dictionary<string, string> { { "$id", puid } };

            var resp = await dg.NewTransaction().QueryWithVars(q, variables);

            var me = resp.Json.ToStringUtf8();

            var expected = JsonSerializer.Serialize(
                JsonSerializer.Deserialize<object>(@"{
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
                }"), new JsonSerializerOptions { WriteIndented = false });

            Equal(expected, me);
        }
        finally
        {
            await ClearDB();
        }
    }

    [Fact]
    public async Task TxnMutateBytesTest()
    {
        var dg = GetDgraphClient();

        try
        {
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

            var pb = JsonSerializer.Serialize(p);

            var mu = new Mutation { CommitNow = true, SetJson = ByteString.CopyFromUtf8(pb) };
            await dg.NewTransaction().Mutate(mu);

            const string q = @"
                    {
                        q(func: eq(name, ""Alice-new"")) {
                            name
                            dgraph.type
                            raw_bytes
                        }
                    }
                ";

            var resp = await dg.NewTransaction().Query(q);

            var me = resp.Json.ToStringUtf8();

            const string expected = @"{""q"":[{""name"":""Alice-new"",""dgraph.type"":[""Person""],""raw_bytes"":""cmF3X2J5dGVz""}]}";

            Equal(expected, me);
        }
        finally
        {
            await ClearDB();
        }
    }

    [Fact]
    public async Task TxnQueryUnmarshalTest()
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
                    p,
                    new Person{Name = "Charlie", Age = 29, DTypes = new[]{ "Person"}}
                },
                Schools = new[] { new School { Name = "Crown Public School", DTypes = new[] { "Institution" } } },
            };

            var txn = dg.NewTransaction();
            var mu = new Mutation();
            var pb = JsonSerializer.Serialize(p);
            mu.SetJson = ByteString.CopyFromUtf8(pb);
            mu.CommitNow = true;
            var response = await txn.Mutate(mu);

            // Assigned uids for nodes which were created would be returned in the response.Uids map.
            var puid = "Alice";
            var variables = new Dictionary<string, string> { { "$id", puid } };
            const string q = @"
                query Me($id: string) {
                    me(func: eq(name, $id)) {
                        name
                        age
                        married
                        raw_bytes
                        friend @filter(eq(name, ""Bob"")) {
                            name
                            age
                            dgraph.type
                        }
                        school {
                            name
                            dgraph.type
                        }
                        dgraph.type
                    }
                }";

            var resp = await dg.NewTransaction().QueryWithVars(q, variables);

            var me = resp.Json.ToStringUtf8();

            var expected = JsonSerializer.Serialize(
                JsonSerializer.Deserialize<object>(@"{
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
                }"), new JsonSerializerOptions { WriteIndented = false }
            );

            Equal(expected, me);
        }
        finally
        {
            await ClearDB();
        }
    }

    [Fact]
    public async Task TxnQueryBestEffortTest()
    {
        try
        {
            var dg = GetDgraphClient();

            var response = await dg.NewTransaction().Mutate(new Mutation {
                SetNquads = ByteString.CopyFromUtf8("_:alice <name> \"Alice\" ."),
                CommitNow = true
            });

            var uid = response.Uids.TryGetValue("alice", out var aid) ? aid : response.Uids.Values.Last();

            var txn = dg.NewTransaction(true, true);
            var resp = await txn.Query($"{{ q(func: uid({uid})) {{ uid }} }}");

            Equal(@$"{{""q"":[{{""uid"":""{uid}""}}]}}", resp.Json.ToStringUtf8());
        }
        finally
        {
            await ClearDB();
        }
    }

    private class SSchool : IEntity
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } //`json:"name,omitempty"`

        [JsonPropertyName("school|since")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime? Since { get; set; } // time.Time `json:"school|since,omitempty"`

        [JsonPropertyName("uid")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Uid Id { get; set; } = Uid.NewUid();

        [JsonPropertyName("dgraph.type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string[] DgraphType { get; set; } = new[] { "Institution" };
    }

    class SPerson : IEntity
    {
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Name { get; set; } // `json:"name,omitempty"`

        [JsonPropertyName("name|origin")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string NameOrigin { get; set; } // `json:"name|origin,omitempty"`

        [JsonPropertyName("friends")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public SPerson[] Friends { get; set; } = new SPerson[0]; // `json:"friends,omitempty"`

        // These are facets on the friend edge.
        [JsonPropertyName("friend|since")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime? Since { get; set; } // time.Time `json:"friends|since,omitempty"`

        [JsonPropertyName("friend|family")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Family { get; set; } // `json:"friends|family,omitempty"`

        [JsonPropertyName("friend|age"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public double Age { get; set; } // `json:"friends|age,omitempty"`

        [JsonPropertyName("friend|close"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Close { get; set; } = false; // `json:"friends|close,omitempty"`

        [JsonPropertyName("school"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public SSchool[] School { get; set; } //`json:"school,omitempty"`

        [JsonPropertyName("uid"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Uid Id { get; set; } = Uid.NewUid();

        [JsonPropertyName("dgraph.type"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string[] DgraphType { get; set; } = new[] { "Person" }; //string `json:"dgraph.type,omitempty"`

    }

    //#if !DISABLED
    [Fact]
    public async Task TxnMutateFacetsTest()
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
                        name_origin: string .
                        since: string .
                        family: string .
                        close: bool .
                        school: [uid] .
                        friends: [uid] .
                        type Person {
                            name
                            age
                            married
                            name_origin
                            since
                            family
                            school
                            close
                            friends
                        }
                        type Institution {
                            name
                            since
                        }
                    "
            };

            await dg.Alter(op);

            var ti = new DateTime(2009, 11, 10, 23, 0, 0, 0, DateTimeKind.Utc);

            var p = new SPerson
            {
                Id = "_:alice",
                Name = "Alice",
                NameOrigin = "Indonesia",
                Friends = new[] {
                    new SPerson {
                        Name = "Bob",
                        Since = ti,
                        Family = "yes",
                        Age = 13,
                        Close = true
                    },
                    new SPerson {
                        Name = "Charlie",
                        Family = "maybe",
                        Age = 16
                    },
                },
                School = new[] {
                    new SSchool {
                        Name = "Wellington School",
                        Since = ti
                    }
                }
            };

            var serialized = JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = false });

            try
            {
                var mu = new Mutation { SetJson = ByteString.CopyFromUtf8(serialized), CommitNow = true };

                Response response = await dg.NewTransaction().Mutate(mu);

                var auid = "Alice";

                var variables = new Dictionary<string, string> { { "$id", auid } };

                const string q = @"
                        query Me($id: string) {
                            me(func: eq(name, $id)) {
                                name @facets
                                dgraph.type
                                friends @filter(eq(name, ""Bob"")) @facets {
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

                var expected = JsonSerializer.Serialize(
                    JsonSerializer.Deserialize<object>(@"
                    {
                        ""me"": [
                            {
                                ""name|origin"": ""Indonesia"",
                                ""name"": ""Alice"",
                                ""dgraph.type"": [
                                    ""Person""
                                ],
                                ""friends"": [
                                    {
                                        ""name"": ""Bob"",
                                        ""dgraph.type"": [
                                            ""Person""
                                        ]
                                    }
                                ],
                                ""school"": [
                                    {
                                        ""name"": ""Wellington School"",
                                        ""dgraph.type"": [
                                            ""Institution""
                                        ],
                                        ""school|since"": ""2009-11-10T23:00:00Z""
                                    }
                                ]
                            }
                        ]
                    }"), new JsonSerializerOptions { WriteIndented = false }
                );

                Equal(expected, me);
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Error.WriteLine(serialized);
                Console.ResetColor();
                throw;
            }
        }
        finally
        {
            await ClearDB();
        }
    }
    //#endif

    [Fact]
    public async Task TxnMutateVarsTest()
    {
        var dg = GetDgraphClient();

        try
        {
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
                Location = JsonSerializer.Serialize(new Point(new Position(1.1, 2))),
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
            var pb = JsonSerializer.Serialize(p);

            mu.SetJson = ByteString.CopyFromUtf8(pb);
            var response = await dg.NewTransaction().Mutate(mu);

            // Assigned uids for nodes which were created would be returned in the response.Uids map.
            var puid = response.Uids.TryGetValue("alice", out var aid) ? aid : response.Uids.Values.Last();

            var variables = new Dictionary<string, string> { { "$name", "Alice" } };

            var q = @"
                    query Me($name: string){
                        me(func: eq(name, $name)) {
                            friend(orderasc: name) {
                                name
                            }
                        }
                    }
                ";

            var resp = await dg.NewTransaction().QueryWithVars(q, variables);

            var me = resp.Json.ToStringUtf8();

            var expected = @"{""me"":[{""friend"":[{""name"":""Bob""},{""name"":""Charlie""}]}]}";

            Equal(expected, me);

            q = @"
                    {
                        charlie(func: eq(name, ""Charlie"")) {
                            uid
                        }
                    }
                ";

            resp = await dg.NewTransaction().Query(q);

            var charlie = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>[]>>(resp.Json.ToStringUtf8())["charlie"][0]["uid"];

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
                    query Me($name: string){
                        me(func: eq(name, $name)) {
                            friend {
                                name
                            }
                        }
                    }
                ";

            resp = await dg.NewTransaction().QueryWithVars(q, variables);

            me = resp.Json.ToStringUtf8();

            expected = @"{""me"":[{""friend"":[{""name"":""Bob""}]}]}";

            Equal(expected, me);
        }
        finally
        {
            await ClearDB();
        }
    }
}
