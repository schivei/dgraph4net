# Dgraph4Net

This client are based on [Dgraph Go client](goclient).\
This README is based on [Dgraph.net README](dgraph.net).

[goclient]: https://github.com/dgraph-io/dgo
[dgraph.net]: https://github.com/dgraph-io/dgraph.net

Before using this client, we highly recommend that you go through [docs.dgraph.io],
and understand how to run and work with Dgraph.

[docs.dgraph.io]:https://docs.dgraph.io

## Table of contents

  - [Packages](#packages)
  - [Install](#install)
  - [Supported Versions](#supported-versions)
  - [Using a Client](#using-a-client)
    - [Creating a Client](#creating-a-client)
    - [Mapping Classes](#mapping-classes)
    - [Altering the Database](#altering-the-database)
      - [Secure DropAll](#secure-dropall)
    - [Creating a Transaction](#creating-a-transaction)
    - [Running a Mutation](#running-a-mutation)
    - [Running a Query](#running-a-query)
    - [Running an Upsert: Query + Mutation](#running-an-upsert-query-mutation)
    - [Committing a Transaction](#committing-a-transaction)
    - [ASP.NET Identity](#asp.net-identity)
    - [Uid propagation after Mutation](#uid-propagation-after-mutation)
    - [In Development](#in-development)

## Packages
- **Dgraph4Net**: [![NuGet](https://img.shields.io/nuget/v/DGraph4Net?style=flat)](https://www.nuget.org/packages/Dgraph4Net/)
- **Dgraph4Net.Core**: [![NuGet](https://img.shields.io/nuget/v/Dgraph4Net.Core?style=flat)](https://www.nuget.org/packages/Dgraph4Net.Core/)
- **DGraph4Net.Identity**: [![NuGet](https://img.shields.io/nuget/v/DGraph4Net.Identity?style=flat)](https://www.nuget.org/packages/DGraph4Net.Identity/)
- **Dgraph4Net.Identity.Core**: [![NuGet](https://img.shields.io/nuget/v/Dgraph4Net.Identity.Core?style=flat)](https://www.nuget.org/packages/Dgraph4Net.Identity.Core/)

![Package Publisher](https://github.com/schivei/dgraph4net/workflows/Package%20Publisher/badge.svg)

## Install

Install using nuget:

```sh
dotnet add package Dgraph4Net
dotnet add package Dgraph4Net.Core
dotnet add package Dgraph4Net.Identity
dotnet add package Dgraph4Net.Identity.Core
```

> The package Dgraph4Net already references Dgraph4Net.Core.\
> The package Dgraph4Net.Identity.Core already references Dgraph4Net.\
> The package Dgraph4Net.Identity already references Dgraph4Net.Identity.Core.

## Supported Versions

Dgraph version   | Dgraph4Net version | dotnet Version
---------------  | ------------------ | --------------
  1.1.Y          |  0.3.Y             | Standard 2.1
  20.03.Y        |  1.X.Y             | .NET5

## Using a Client

### Creating a Client

Make a new client by passing in one or more GRPC channels pointing to alphas.

```c#
var channel = new Channel("localhost:9080", ChannelCredentials.Insecure);
var client = new Dgraph4NetClient(channel);
```


### Mapping Classes

Mapping classes can perform a schema migration and marshaling.

```c#
// just map classes
ClassFactory.MapAssembly(typeof(DUser).Assembly);
```

**\*\*NOTE**: your classes need to implement `Dgraph4Net.IEntity` interface.

### Altering the Database

To set the schema, pass the assembly that contains your mapped classes to client map, as seen below:

```c#
// migrate schema
dgraph.Map(typeof(DUser).Assembly);
```

The returned result is a `StringBuilder` with schema, this schema also been writed on project/binary folder with the name `schema.dgraph`, do not put it into version control.\
The `schema.dgraph` file is used to reduce `Alter` on database, likes a migration.

**\*\*NOTE**: your classes need to implement `Dgraph4Net.IEntity` interface.

You also can use the declarative form, as seen below:

```c#
var schema = "`name: string @index(exact) .";
await client.Alter(new Operation{ Schema = schema });
```

#### Secure DropAll

The Dgraph4Net prevents dgraph objects (types and attributes) to be deleted when DropAll.

To perform DropAll:

```c#
var op = new Operation { DropAll = true };
await client.Alter(op);
```

To perform original dgraph method and drop all including dgraph objects:
```c#
var op = new Operation { DropAll = true, AlsoDropDgraphSchema = true };
await client.Alter(op);
```

### Creating a Transaction

To create a transaction, call `Dgraph4NetClient.NewTransaction` method, which returns a
new `Transaction` object. This operation incurs no network overhead.

It is good practice to call to wrap the `Transaction` in a `using` block, so that the `Transaction.Dispose` function is called after running
the transaction. Or you can use it on `IDisposable` services injected as `Transient` on your application.

```c#
await using var txn = client.NewTransaction();
...
```

You can also create Read-Only transactions. Read-Only transactions only allow querying, and can be created using `DgraphClient.NewReadOnlyTransaction`.


### Running a Mutation

`Transaction.Mutate(RequestBuilder)` runs a mutation. It takes in a json mutation string.

We define a person object to represent a person and serialize it to a json mutation string. In this example, we are using the [JSON.NET](https://www.newtonsoft.com/json) library, but you can use any JSON serialization library you prefer.

```c#
await using var txn = client.NewTransaction();
var alice = new Person{ Name = "Alice" };
var json = JsonConvert.SerializeObject(alice);
var mutation = new Mutation
{
    CommitNow = true,
    DeleteJson = ByteString.CopyFromUtf8(json)
};
    
var response = await txn.Mutate(mutation);
```

You can also set mutations using RDF format, if you so prefer, as seen below:

```c#
var rdf = "_:alice <name> \"Alice\"";
var mutation = new Mutation
{
    CommitNow = true,
    SetNquads = rdf
};
var response = await txn.Mutate(mutation);
```

Check out the tests as example in [`tests/DGraph4Net.Tests`](tests/DGraph4Net.Tests).

### Running a Query

You can run a query by calling `Transaction.Query(string)`. You will need to pass in a
GraphQL+- query string. If you want to pass an additional map of any variables that
you might want to set in the query, call `Transaction.QueryWithVars(string, Dictionary<string,string>)` with
the variables dictionary as the second argument.

The response would contain the response string.

Let’s run the following query with a variable $a:

```console
query all($a: string) {
  all(func: eq(name, $a))
  {
    name
  }
}
```

Run the query, deserialize the result from Uint8Array (or base64) encoded JSON and
print it out:

```c#
// Run query.
var query = @"query all($a: string) {
  all(func: eq(name, $a))
  {
    name
  }
}";

var vars = new Dictionary<string,string> { { $a: "Alice" } };
var res = await client.NewTransaction(true, true).QueryWithVars(query, vars);

// Print results.
Console.Write(res.Json);
```

### Running an Upsert: Query + Mutation

The `Transaction.Mutate` function allows you to run upserts consisting of one query and one mutation. 

To know more about upsert, we highly recommend going through the docs at https://docs.dgraph.io/mutations/#upsert-block.

```c#
var q = $@"
    query Q($email: string) {{
        u as var(func: eq(email, $email))
    }}";

var request = new Request{ Query = query, CommitNow = true };

request.Vars.Add("$userName", "email@dgraph.io");

var mutation = new Mutation{
  SetNquads = @"`uid(user) <email> ""email@dgraph.io"" .",
  Cond = "@if(eq(len(u), 0))",
  CommitNow = true,
};

request.Mutations.Add(mutation);

// Upsert: If email not found, perform a new mutation, or else do nothing.
await txn.Do(request);
```

### Committing a Transaction

A transaction can be committed using the `Transaction.Commit` method. If your transaction
consisted solely of calls to `Transaction.Query` or `Transaction.QueryWithVars`, and no calls to
`Transaction.Mutate`, then calling `Transaction.Commit` is not necessary.

An error will be returned if other transactions running concurrently modify the same
data that was modified in this transaction. It is up to the user to retry
transactions when they fail.

```c#
await using var txn = client.NewTransaction();
try
{
  var rdf = "_:alice <name> \"Alice\"";
  var mutation = new Mutation { SetNquads = rdf };
  var response = await txn.Mutate(mutation);
  txn.Commit();
}
catch
{
  txn.Abort(); // for SQL users - this is like a ROLLBACK TRANSACTION
  throw;
}
```

### ASP.NET Identity

You can use ASP.NET Identity for a complete native ASP.NET user access security.

Check out the example code [`examples/DGraph4Net.Identity.Example`](examples/DGraph4Net.Identity.Example).


### Uid propagation after Mutation

DGraph4Net propagates for all instances of Uid struct when a Mutation are performed.
To ensure this functionality make sure you are initializing Uid instance.

```c#
class MyClass {
  Uid Id { get; set; } = Uid.NewUid();
}

....
await client.Mutate(mutation);
```

When mutation occurs, all instances of Uid returned by dgraph mutation are updated with the real Uid, 
it is useful for deep id propagation when you send many objects to dgraph and 
reduces the async calling to check an object propagation to database or making new queries 
to retieve the last inserted data or navigating to Uids property returned from mutation.


### In Development

* Documentation - More, more and more
* Linq to DQL - Linq support for queries
* Tenant implementation examples - Physical and Logical tenants examples
