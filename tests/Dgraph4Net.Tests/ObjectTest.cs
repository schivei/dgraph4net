using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Api;

using Google.Protobuf;

using Xunit;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using NetGeo.Json;
using Dgraph4Net.ActiveRecords;

namespace Dgraph4Net.Tests;

[Collection("Dgraph4Net")]
public class ObjectTest : ExamplesTest
{
    #region bootstrap
    private class School : IEntity
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("dgraph.type")]
        public string[] DgraphType { get; set; }

        [JsonProperty("since")]
        public DateTime Since { get; set; }

        [JsonProperty("uid")]
        public Uid Uid { get; set; } = Uid.NewUid();
    }

    private class SchoolMap : ClassMap<School>
    {
        protected override void Map()
        {
            SetType("School");
            String(x => x.Name, "name");
            DateTime(x => x.Since, "since");
        }
    }

    private class Person : IEntity
    {
        [JsonProperty("uid")]
        public Uid Uid { get; set; } = Uid.NewUid();

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
        public string Location { get; set; }

        [JsonProperty("school")]
        public School[] Schools { get; set; }

        [JsonProperty("dgraph.type")]
        public string[] DgraphType { get; set; }

        [JsonProperty("name_origin")]
        public string NameOrigin { get; set; }

        [JsonProperty("since")]
        public DateTimeOffset Since { get; set; }

        [JsonProperty("family")]
        public string Family { get; set; }

        [JsonProperty("close")]
        public bool Close { get; set; }
    }

    private class PersonMap : ClassMap<Person>
    {
        protected override void Map()
        {
            SetType("Person");
            String(x => x.Name, "name");
            Integer(x => x.Age, "age");
            DateTime(x => x.Dob, "dob");
            Boolean(x => x.Married, "married");
            String(x => x.Raw, "raw_bytes");
            HasMany(x => x.Friends, "friend");
            String(x => x.Location, "loc");
            HasMany(x => x.Schools, "school");
            String(x => x.NameOrigin, "name_origin");
            DateTime(x => x.Since, "since");
            String(x => x.Family, "family");
            Boolean(x => x.Close, "close");
        }
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
                DgraphType = new[] { "Person" },
                Location = JsonConvert.SerializeObject(new Point { Coordinates = new[] { 1.1, 2d } }),
                Dob = dob,
                Raw = Encoding.UTF8.GetBytes("raw_bytes"),
                Friends = new[]{new Person {
                    Uid = "_:bob",
                    Name =  "Bob",
                    Age =   24,
                    DgraphType = new []{"Person"},
                }, new Person{
                    Uid = "_:charlie",
                    Name =  "Charlie",
                    Age =   29,
                    DgraphType = new []{"Person"},
                }},
                Schools = new[]{ new School{
                    Uid = "_:school",
                    Name =  "Crown Public School",
                    DgraphType = new []{"Institution"},
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

            var json = JsonConvert.SerializeObject(p);

            mu.SetJson = ByteString.CopyFromUtf8(json);

            var response = await dg.NewTransaction().Mutate(mu);

            // Assigned uids for nodes which were created would be returned in the response.Uids map.
            var vars = new VarTriples();
            vars.Add(new("id1", "Alice"));
            var variables = vars.ToDictionary();
            var q = @$"query Me({vars.ToQueryString()}){{
                me(func: eq({DType<Person>.Predicate(p => p.Name)}, $id1), first: 1) {{
                    {DType<Person>.Predicate(p => p.Name)}
                    {DType<Person>.Predicate(p => p.Dob)}
                    {DType<Person>.Predicate(p => p.Age)}
                    {DType<Person>.Predicate(p => p.Married)}
                    {DType<Person>.Predicate(p => p.Raw)}
                    {DType<Person>.Predicate(p => p.Friends)} @filter(eq({DType<Person>.Predicate(p => p.Name)}, ""Bob"")){{
                        {DType<Person>.Predicate(p => p.Name)}
                        {DType<Person>.Predicate(p => p.Age)}
                        dgraph.type
                    }}
                    loc
                    {DType<Person>.Predicate(p => p.Schools)} {{
                        {DType<School>.Predicate(p => p.Name)}
                        dgraph.type
                    }}
                    dgraph.type
                }}
            }}";

            var resp = await dg.NewTransaction().QueryWithVars(q, variables);

            var expected = @"{""me"":[{""name"":""Alice"",""dob"":""1980-01-01T23:00:00Z"",""age"":26,""married"":true,""raw_bytes"":""cmF3X2J5dGVz"",""friend"":[{""name"":""Bob"",""age"":24,""dgraph.type"":[""Person""]}],""loc"":{""type"":""Point"",""coordinates"":[1.1,2]},""school"":[{""name"":""Crown Public School"",""dgraph.type"":[""Institution""]}],""dgraph.type"":[""Person""]}]}";

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

            var p = new Person { Name = "Alice", DgraphType = new[] { "Person" } };

            var mu = new Mutation { CommitNow = true };
            var pb = JsonConvert.SerializeObject(p);

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
                DgraphType = new[] { "Person" },
                Location = JsonConvert.SerializeObject(new Point { Coordinates = new[] { 1.1, 2d } }),
                Raw = Encoding.UTF8.GetBytes("raw_bytes"),
                Friends = new[]
                {
                    new Person {Name = "Bob", Age = 24, DgraphType = new[] {"Person"}},
                    new Person {Name = "Charlie", Age = 29, DgraphType = new[] {"Person"}}
                },
                Schools = new[] { new School { Name = "Crown Public School", DgraphType = new[] { "Institution" } } },
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
            var pb = JsonConvert.SerializeObject(p);

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

            var expected = @"{""me"":[{""name"":""Alice"",""age"":26,""married"":true,""raw_bytes"":""cmF3X2J5dGVz"",""friend"":[{""name"":""Bob"",""age"":24,""dgraph.type"":[""Person""]}],""loc"":{""type"":""Point"",""coordinates"":[1.1,2]},""school"":[{""name"":""Crown Public School"",""dgraph.type"":[""Institution""]}],""dgraph.type"":[""Person""]}]}";

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
                DgraphType = new[] { "Person" },
                Raw = Encoding.UTF8.GetBytes("raw_bytes"),
            };

            var pb = JsonConvert.SerializeObject(p);

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
                DgraphType = new[] { "Person" },
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
                DgraphType = new[] { "Person" },
                Raw = Encoding.UTF8.GetBytes("raw_bytes"),
                Friends = new[]{
                    p,
                    new Person{Name = "Charlie", Age = 29, DgraphType = new[]{ "Person"}}
                },
                Schools = new[] { new School { Name = "Crown Public School", DgraphType = new[] { "Institution" } } },
            };

            var txn = dg.NewTransaction();
            var mu = new Mutation();
            var pb = JsonConvert.SerializeObject(p);
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

            var expected = @"{""me"":[{""name"":""Alice"",""age"":26,""married"":true,""raw_bytes"":""cmF3X2J5dGVz"",""friend"":[{""name"":""Bob"",""age"":24,""dgraph.type"":[""Person""]}],""school"":[{""name"":""Crown Public School"",""dgraph.type"":[""Institution""]}],""dgraph.type"":[""Person""]}]}";

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

            var response = await dg.NewTransaction().Mutate(new Mutation
            {
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
        [JsonProperty("name")]
        public string Name { get; set; } //`json:"name,omitempty"`

        [JsonProperty("school|since")]
        public DateTime? Since { get; set; } // time.Time `json:"school|since,omitempty"`

        [JsonProperty("uid")]
        public Uid Uid { get; set; } = Uid.NewUid();

        [JsonProperty("dgraph.type")]
        public string[] DgraphType { get; set; } = new[] { "Institution" };

        public bool ShouldSerializeSince() => Since.HasValue;
    }

    class SPerson : IEntity
    {
        [JsonProperty("name")]
        public string Name { get; set; } // `json:"name,omitempty"`

        [JsonProperty("name|origin")]
        public string NameOrigin { get; set; } // `json:"name|origin,omitempty"`

        [JsonProperty("friends")]
        public SPerson[] Friends { get; set; } = new SPerson[0]; // `json:"friends,omitempty"`

        // These are facets on the friend edge.
        [JsonProperty("friend|since")]
        public DateTime? Since { get; set; } // time.Time `json:"friends|since,omitempty"`

        public bool ShouldSerializeSince() => Since.HasValue;

        [JsonProperty("friend|family")]
        public string Family { get; set; } // `json:"friends|family,omitempty"`

        [JsonProperty("friend|age")]
        public double Age { get; set; } // `json:"friends|age,omitempty"`

        [JsonProperty("friend|close")]
        public bool Close { get; set; } = false; // `json:"friends|close,omitempty"`

        [JsonProperty("school")]
        public SSchool[] School { get; set; } //`json:"school,omitempty"`

        [JsonProperty("uid")]
        public Uid Uid { get; set; } = Uid.NewUid();

        [JsonProperty("dgraph.type")]
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
                Uid = "_:alice",
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

            var serialized = JsonConvert.SerializeObject(p);

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

                var expected = @"{""me"":[{""name|origin"":""Indonesia"",""name"":""Alice"",""dgraph.type"":[""Person""],""friends"":[{""name"":""Bob"",""dgraph.type"":[""Person""]}],""school"":[{""name"":""Wellington School"",""dgraph.type"":[""Institution""],""school|since"":""2009-11-10T23:00:00Z""}]}]}";

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
                DgraphType = new[] { "Person" },
                Location = JsonConvert.SerializeObject(new Point { Coordinates = new[] { 1.1d, 2 } }),
                Raw = Encoding.UTF8.GetBytes("raw_bytes"),
                Friends = new[]
                {
                    new Person {Name = "Bob", Age = 24, DgraphType = new[] {"Person"}},
                    new Person {Name = "Charlie", Age = 29, DgraphType = new[] {"Person"}}
                },
                Schools = new[] { new School { Name = "Crown Public School", DgraphType = new[] { "Institution" } } },
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
            var pb = JsonConvert.SerializeObject(p);

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
                    query Me($name: string){
                        me(func: eq(name, $name)) {
                            friend(orderasc: name) @filter(eq(name, ""Bob"")) {
                                name
                            }
                        }
                    }
                ";

            // because of data propagation delay, we need to wait a bit before querying
            await Task.Delay(100);

            resp = await dg.NewTransaction().QueryWithVars(q, variables);

            me = resp.Json.ToStringUtf8();

            var maxTests = 300; // to limit the number of tests with ~30 seconds timeout

            while (me == expected && maxTests-- > 0)
            {
                mu = new Mutation { CommitNow = true, DelNquads = ByteString.CopyFromUtf8(nq) };

                req = new Request { Query = @"
                query Q($charlie: string, $alice: string) {
                    c as var(func: uid($charlie))
                    a as var(func: uid($alice))
                }", CommitNow = true };
                req.Vars.Add("$charlie", charlie);
                req.Vars.Add("$alice", puid);
                mu.Cond = "@if(eq(len(a), 1) AND eq(len(c), 1))";
                req.Mutations.Add(mu);
                await using var txn = dg.NewTransaction();
                await txn.Do(req);

                // because of data propagation delay, we need to wait a bit before querying
                await Task.Delay(100);

                await using var txn2 = dg.NewTransaction();

                resp = await txn2.QueryWithVars(q, variables);

                me = resp.Json.ToStringUtf8();
            }

            var expected2 = @"{""me"":[{""friend"":[{""name"":""Bob""}]}]}";

            Equal(expected2, me);
        }
        finally
        {
            await ClearDB();
        }
    }
}
