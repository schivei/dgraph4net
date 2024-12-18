# Dgraph4Net

This client is based on [Dgraph Go client](https://github.com/dgraph-io/dgo).\
This README is based on [Dgraph.net README](https://github.com/dgraph-io/dgraph.net).

Before using this client, we highly recommend that you go through [docs.dgraph.io](https://docs.dgraph.io),
and understand how to run and work with Dgraph.

## Table of contents

  - [Packages](#packages)
  - [Install](#install)
  - [Supported Versions](#supported-versions)
  - [Using a Client](#using-a-client)
    - [Creating a Client](#creating-a-client)
      - [Using DI](#using-di)
    - [Mapping Classes](#mapping-classes)
      - [Creating mappings](#creating-mappings)
      - [Using Facets](#using-facets)
        - [With Attribute](#with-attribute)
        - [With Type](#with-type)
        - [With direct access](#with-direct-access)
    - [Creating a Transaction](#creating-a-transaction)
    - [Running a Mutation](#running-a-mutation)
    - [Running a Query](#running-a-query)
    - [Running an Upsert: Query + Mutation](#running-an-upsert-query--mutation)
    - [Committing a Transaction](#committing-a-transaction)
    - [Uid propagation after Mutation](#uid-propagation-after-mutation)
  - [Migrations](#migrations)
    - [Creating a Migration](#creating-a-migration)
    - [Applying a Migration](#applying-a-migration)
    - [Removing a Migration](#removing-a-migration)
  - [In Development](#in-development)
  - [Notes](#notes)

## Packages
- **Dgraph4Net.Newtonsoft.Json**: [![NuGet](https://img.shields.io/nuget/v/DGraph4Net.Newtonsoft.Json?style=flat)](https://www.nuget.org/packages/Dgraph4Net.Newtonsoft.Json/)
- **Dgraph4Net**: [![NuGet](https://img.shields.io/nuget/v/DGraph4Net?style=flat)](https://www.nuget.org/packages/Dgraph4Net/)
- **Dgraph4Net.Core**: [![NuGet](https://img.shields.io/nuget/v/Dgraph4Net.Core?style=flat)](https://www.nuget.org/packages/Dgraph4Net.Core/)

### Tools
- **Dgraph4Net.Tools**: [![NuGet](https://img.shields.io/nuget/v/DGraph4Net.Tools?style=flat)](https://www.nuget.org/packages/Dgraph4Net.Tools/)
  - For migrations and schema management

![Package Publisher](https://github.com/schivei/dgraph4net/workflows/Package%20Publisher/badge.svg)

## Install

Install using nuget:

```sh
dotnet add package Dgraph4Net.Newtonsoft.Json
dotnet add package Dgraph4Net
dotnet add package Dgraph4Net.Core
dotnet tool install Dgraph4Net.Tools
```

> The json packages already references Dgraph4Net.\
> The package Dgraph4Net already references Dgraph4Net.Core.\
> The package tool Dgraph4Net.Tools references Dgraph4Net for code generation.

## Supported Versions

| Dgraph version | Dgraph4Net version | dotnet Version |
|----------------|--------------------|----------------|
| 1.1.Y          | 0.3.Y              | Standard 2.1   |
| 20.03.Y        | 1.X.Y              | .NET5          |
| 21.2.Y         | 2022.X.Y           | .NET6          |
| 22.0.Y         | 2023.2.<145X.Y     | .NET7          |
| 23.0.Y         | 2023.2.>145X.Y     | .NET7          |
| 24.0.Y         | 2024.X.Y           | .NET8          |

## Using a Client

### Creating a Client

Make a new client by passing in one or more GRPC channels pointing to alphas.

```c#
var channel = new Channel("localhost:9080", ChannelCredentials.Insecure);
var client = new Dgraph4NetClient(channel);
```

#### Using DI

You can use DI to create a client, as seen below:

Add the following line to your `appsettings.json` file:
```json
{
  "ConnectionStrings": {
    "DgraphConnection": "server=<host_port:9080>;[user id=<username>];[password=<password>];[use tls=<false to insecure is default, no SSL validation>];[api-key=<cloud bearer api key>]"
  }
}
```

Add the following line to your Startup.cs file:
```c#
services.AddDgraph(); // for DefaultConnection string
// or
services.AddDgraph("DgraphConnection"); // for named connection string
// or
services.AddDgraph(sp => "_inline_cs"); // for inline connection string
```

### Mapping Classes

Mapping classes can perform a schema migration and marshaling.

```c#
services.AddDgraph(); // already call mapping
// or, for manual mapping
ClassMapping.Map(); // to map all assemblies with classes that implements AEntity
// or
ClassMapping.Map(assemlies); // to map specifics assemblies
```

**\*\*NOTE**: your classes need to implement `Dgraph4Net.AEntity<T>` abstract class.

#### Creating mappings

Follow the example below to create a mapping:

```c#
// poco types
public class Person : AEntity<Person>
{
    public string Name { get; set; }
    public List<Person> BossOf { get; set; } = new List<Person>();
    public Company WorksFor { get; set; }
    public Person? MyBoss { get; set; }
}

public class Company : AEntity<Company>
{
    public string Name { get; set; }
    public CompanyIndustry Industry { get; set; }
    public ICollection<Person> WorksHere { get; set; } = new List<Person>();
}

public enum CompanyIndustry
{
    IT,
    Finance,
    Health,
    Food,
    Other
}

// mappings
internal sealed class PersonMapping : ClassMap<Person>
{
    protected override void Map()
    {
        SetType("Person"); // to set the type name
        String(x => x.Name, "name"); // to map a property to name predicate
        HasOne(x => x.WorksFor, "works_for", true, true); // to map a property to an uid predicate
        HasOne(x => x.MyBoss, "my_boss", true, true); // to map a property to an uid predicate
        HasMany(x => x.BossOf, "my_boss", x => x.MyBoss); // to map a property to reversed my_boss
    }
}

internal sealed class CompanyMapping : ClassMap<Company>
{
    protected override void Map()
    {
        SetType("Company"); // to set the type name
        String(x => x.Name, "name"); // to map a property to name predicate
        String<CompanyIndustry>(x => x.Industry, "industry"); // to map a property to string predicate that transforms enum to string
        HasMany(x => x.WorksHere, "works_for", x => x.WorksFor); // to map a property to reversed works_for
    }
}
```

#### Using Facets

Facets are a way to store metadata for predicates. They are key-value pairs that can be associated with a predicate. Facets can be used to store information like timestamps, geolocations, and other metadata. Facets are stored as part of the predicate, and are returned with the predicate when queried.

You can use i18n facets too, to store multiple languages in the same predicate.

**Pay Attention**: You MUST NOT map facets.

##### With Attribute

```csharp
class MyClass1 : AEntity<MyClass1> {
    public List<MyClass2> Classes { get; set; }
}

class MyClass2 : AEntity<MyClass2> {
    [Facet<MyClass1>("since", nameof(MyClass1.Classes))]
    public DateTimeOffset Since
    {
        get => GetFacet(DateTimeOffset.MinValue);
        set => SetFacet<DateTimeOffset>(value);
    }
}
```

##### With Type

You can use the `FacetPredicate<T, TE>` on your predicate or create a custom one likes:

```csharp
class MyCustomFacets(MyClass3 instance, string value = default) : FacetPredicate<MyClass3, string>(instance, property => property.Name, value) {
    public string Origin
    {
        get => GetFacet("origin", "american");
        set => SetFacet("origin", value);
    }
}

class MyClass3 : AEntity<MyClass3> {
    public MyCustomFacets Name { get; set; }
}
```

**Note**: Custom facets must have a constructor with two arguments (in order): ClassType for instance and the value of predicate.

##### With direct access

Use it if you need to gets or sets any facet inside an object.

```csharp
...
var mc = new MyClass();

// - Getter
mc.GetFacet("property|facet", defaultValue);
mc.GetFacet(m => m.Property, "facet", defaultValue);

// - Setter
mc.SetFacet("property|facet", value);
mc.SetFacet(m => m.Property, "facet", value);

// - i18n, use an 'at' (@) instead of a 'pipe' (|); if using expression selection, put the 'at' at the start of facet name
// - Getter
mc.GetFacet("property@es", defaultValue);
mc.GetFacet(m => m.Property, "@es", defaultValue);

// - Setter
mc.SetFacet("property@es", value);
mc.SetFacet(m => m.Property, "@es", value);
...
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
var json = JsonSerializer.Serialize(alice);
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

Check out the tests as example in [`tests`](tests) or [`examples`](examples) folders.

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

You can also create queries with helpers, as seen below:

```c#
var vars = new VarTriples();
vars.Add(new("id1", "Alice"));

var query = @$"query Me({vars.ToQueryString()}){{
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

var res = await client.NewTransaction(true, true).QueryWithVars(query, vars.ToDictionary());

// Print results.
Console.Write(res.Json);
```

It is useful to prevent predicate and types typos, and also to help with refactoring.

### Running an Upsert: Query + Mutation

The `Transaction.Mutate` function allows you to run update inserts consisting of one query and one mutation.

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

### Uid propagation after Mutation

DGraph4Net propagates for all instances of Uid struct when a Mutation are performed.
To ensure this functionality make sure you are initializing Uid instance.

```c#
class MyClass {
  Uid Id { get; set; } = Uid.NewUid(); // is important to every set Id to Uid.NewUid() on construct a type
}

....
await client.Mutate(mutation);
```

When mutation occurs, all instances of Uid returned by dgraph mutation are updated with the real Uid,
it is useful for deep id propagation when you send many objects to dgraph and
reduces the async calling to check an object propagation to database or making new queries
to retrieve the last inserted data or navigating to Uid property returned from mutation.

## Migrations

Dgraph4Net has a migration system that allows you to create and update your database schema.

You need to install the package tool `Dgraph4Net.Tools` to use the migration system.

### Creating a Migration

To create a migration, you need to create a class that inherits from `Migration` and implement the `Up` and `Down` method.

```bash
dotnet tool install --global Dgraph4Net.Tools
```

To immediately run database update you can use the `-u` or `--update` option.

```bash
dgn migration add MyMigrationName -o Migrations --server server:port --project MyProject.csproj [--uid <user_id>] [--pwd <password>] [-u]
```

The command above will create a migration (.cs) and schema (.cs.schema) files in the `Migrations` folder of your project.

The schema file will store the current expected schema for your migration and the others created before.

### Applying a Migration

To apply a migration, you need to run the command below.

```bash
dgn migration up --server server:port --project MyProject.csproj [--uid <user_id>] [--pwd <password>]
```

### Removing a Migration

To remove a migration, you need to run the command below.

```bash
dgn migration remove MyMigrationName -o Migrations --server server:port --project MyProject.csproj [--uid <user_id>] [--pwd <password>]
```

The remove will update the database schema to the previous migration and remove the migration and schema files.

## In Development

* LINQ for dgraph

## Notes

Now we will only update the .NET version when it is a LTS version.
