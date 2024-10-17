using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Api;
using Dgraph4Net.ActiveRecords;
using Google.Protobuf;
using LateApexEarlySpeed.Xunit.Assertion.Json;
using Xunit;

namespace Dgraph4Net.Tests;

[Collection("Dgraph4Net")]
public class ObjectTest : ExamplesTest
{
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

            var bob = new Person
            {
                Uid = "_:bob",
                Age = 24,
                DgraphType = new[] { "Person" },
            };

            bob.Name += "Bob";

            var charlie = new Person
            {
                Uid = "_:charlie",
                Age = 29,
                DgraphType = new[] { "Person" },
            };

            charlie.Name += "Charlie";

            var alice = new Person
            {
                Uid = "_:alice",
                Age = 26,
                Married = true,
                DgraphType = new[] { "Person" },
                Location = new() { Coordinates = new[] { 1.1, 2d } },
                Dob = dob,
                Raw = Encoding.UTF8.GetBytes("raw_bytes"),
                Friends = [bob, charlie],
                Schools = [
                    new School{
                        Uid = "_:school",
                        Name =  "Crown Public School",
                        DgraphType = new []{"Institution"},
                    }
                ],
            };

            alice.Name += "Alice";

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

            mu.SetJson = alice.ToJson();

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

            JsonAssertion.Equivalent(expected, actual);
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

            var p = new Person { DgraphType = new[] { "Person" } };

            p.Name += "Alice";

            var mu = new Mutation { CommitNow = true };

            mu.SetJson = p.ToJson();

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

            JsonAssertion.Equivalent(expected, me);
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

            var bob = new Person { Age = 24, DgraphType = new[] { "Person" } };

            bob.Name += "Bob";

            var charlie = new Person { Age = 29, DgraphType = new[] { "Person" } };

            charlie.Name += "Charlie";

            var p = new Person
            {
                Uid = "_:alice",
                Age = 26,
                Married = true,
                DgraphType = new[] { "Person" },
                Location = new() { Coordinates = new[] { 1.1, 2d } },
                Raw = Encoding.UTF8.GetBytes("raw_bytes"),
                Friends = new[]
                {
                    bob,
                    charlie
                },
                Schools = new[] { new School { Name = "Crown Public School", DgraphType = new[] { "Institution" } } },
            };

            p.Name += "Alice";

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
            mu.SetJson = p.ToJson();

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

            JsonAssertion.Equivalent(expected, me);
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
                DgraphType = new[] { "Person" },
                Raw = Encoding.UTF8.GetBytes("raw_bytes"),
            };

            p.Name += "Alice-new";

            var mu = new Mutation { CommitNow = true, SetJson = p.ToJson() };
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

            JsonAssertion.Equivalent(expected, me);
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
                Age = 24,
                DgraphType = new[] { "Person" },
            };

            p.Name += "Bob";

            // While setting an object if a struct has a Uid then its properties
            // in the graph are updated else a new node is created.
            // In the example below new nodes for Alice and Charlie and school are created
            // (since they dont have a Uid).  Alice is also connected via the friend edge
            // to an existing node Bob.
            p = new Person
            {
                Uid = "_:alice",
                Age = 26,
                Married = true,
                DgraphType = new[] { "Person" },
                Raw = Encoding.UTF8.GetBytes("raw_bytes"),
                Friends = new[]{
                    p,
                    new Person{Age = 29, DgraphType = new[]{ "Person"}}
                },
                Schools = new[] { new School { Name = "Crown Public School", DgraphType = new[] { "Institution" } } },
            };

            p.Name += "Alice";
            p.Friends[1].Name += "Charlie";

            var txn = dg.NewTransaction();
            var mu = new Mutation
            {
                SetJson = p.ToJson(),
                CommitNow = true
            };

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

            JsonAssertion.Equivalent(expected, me);
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

            JsonAssertion.Equivalent(@$"{{""q"":[{{""uid"":""{uid}""}}]}}", resp.Json.ToStringUtf8());
        }
        finally
        {
            await ClearDB();
        }
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
                        since: string .
                        family: string .
                        close: bool .
                        school: [uid] .
                        friend: [uid] .
                        type Person {
                            name
                            age
                            married
                            since
                            family
                            school
                            close
                            friend
                        }
                        type Institution {
                            name
                            since
                        }
                    "
            };

            await dg.Alter(op);

            var ti = new DateTime(2009, 11, 10, 23, 0, 0, 0, DateTimeKind.Utc);

            var bob = new Person
            {
                Uid = "_:bob",
                Since = ti,
            };

            bob.Name += "Bob";

            bob.SetFacet("friend|family", "yes");
            bob.SetFacet(x => x.Friends, "age", 13);
            bob.SetFacet("friend|close", true);

            var charlie = new Person
            {
                Uid = "_:charlie"
            };

            charlie.Name += "Charlie";

            charlie.SetFacet(p => p.Friends, "family", "maybe");
            charlie.SetFacet(p => p.Friends, "age", 16);

            var p = new Person
            {
                Uid = "_:alice",
                Friends = new[] {
                    bob,
                    charlie,
                },
                Schools = new[] {
                    new School {
                        Name = "Wellington School",
                        Since = ti
                    }
                }
            };

            p.Name += "Alice";
            p.Name.Origin = "Indonesia";

            var mu = new Mutation { SetJson = p.ToJson(), CommitNow = true };

            Response response = await dg.NewTransaction().Mutate(mu);

            var auid = "Alice";

            var variables = new Dictionary<string, string> { { "$id", auid } };

            const string q = @"
                    query Me($id: string) {
                        me(func: eq(name, $id)) {
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

            var expected = @"{""me"":[{""name|origin"":""Indonesia"",""name"":""Alice"",""dgraph.type"":[""Person""],""friend"":[{""name"":""Bob"",""dgraph.type"":[""Person""],""friend|age"": 13,""friend|close"": true,""friend|family"": ""yes""}],""school"":[{""name"":""Wellington School"",""dgraph.type"":[""Institution""],""school|since"":""2009-11-10T23:00:00Z""}]}]}";

            JsonAssertion.Equivalent(expected, me);
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
                Age = 26,
                Married = true,
                DgraphType = new[] { "Person" },
                Location = new() { Coordinates = new[] { 1.1d, 2 } },
                Raw = Encoding.UTF8.GetBytes("raw_bytes"),
                Friends = new[]
                {
                    new Person {Age = 24, DgraphType = new[] {"Person"}},
                    new Person {Age = 29, DgraphType = new[] {"Person"}}
                },
                Schools = new[] { new School { Name = "Crown Public School", DgraphType = new[] { "Institution" } } },
            };

            p.Name += "Alice";
            p.Friends[0].Name += "Bob";
            p.Friends[1].Name += "Charlie";

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

            var mu = new Mutation
            {
                CommitNow = true,
                SetJson = p.ToJson()
            };

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

            JsonAssertion.Equivalent(expected, me);

            q = @"
                    {
                        charlie(func: eq(name, ""Charlie"")) {
                            uid
                        }
                    }
                ";

            resp = await dg.NewTransaction().Query(q);

            var charlie = resp.Json.FromJson<Dictionary<string, Dictionary<string, string>[]>>()["charlie"][0]["uid"];

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

            JsonAssertion.Equivalent(expected2, me);
        }
        finally
        {
            await ClearDB();
        }
    }
}
