using System.Linq.Expressions;
using System.Reflection;
using Api;
using Dgraph4Net.ActiveRecords;

namespace Dgraph4Net;

// sample of dql
/*
query Q($p0: string) {
  pid as var(func: type(Person)) @filter(eq(name, $p0) OR (has(~study_here) AND gt(count(~study_here), 0)))
  per(func: type(Person)) @filter(uid(pid)) {
    name
    num_friends: count(friend)
  }
  sch(func: type(School)) {
    name
    ts as count(uid)
    study_here {
      te as count(uid)
      name
    }
  }
  agg() {
    total_of_students: sum(val(te))
    numOf_Schools: sum(val(ts))
  }
}
*/
// sample of c# dql HighLevel query
/*
IDgraph4NetClient _client;
var response = await _client.QueryAsync(q => q
    .Var("pid", v => v
        .Type<Person>()
        .Filter(f => f.Eq(p => p.Name, "Alice") || (f.Has(p => p.StudyHere) && f.Gt(f.Count(p => p.StudyHere), 0)))
    )
    .From<Person>("per", fm => fm
        .Filter(f => f.UidVar("uid"))
        .Select(s => s
            .Predicate(p => p.Name)
            .Alias("num_friends", v => v.Count(p => p.Friends))
        )
    )
    .From<School>("sch", fm => fm
        .Select(s => s
            .Predicate(p => p.Name)
            .Var("ts", v => v.Count(p => p.Id))
            .Predicate(p => p.StudyHere, e => e
                .Var("te", v => v.Count(p => p.Uid))
                .Predicate(p => p.Name)
            )
        )
    )
    .Aggregate("agg", a => a
        .Sum("total_of_students", s => s.Val("te"))
        .Sum("numOf_Schools", s => s.Val("ts"))
    )
);
*/

// generate a query builder to attend the above sample
public static class QueryExtensions
{
    public static Task<Response> Query(this IDgraph4NetClient client, Action<DgraphQueryBuilder> act) =>
        DgraphQueryBuilder.QueryAsync(client, act);

    public static string GetPredicateName(this LambdaExpression prop) =>
        ClassMapping.GetPredicate(prop.GetProperty()).ToTypePredicate();

    public static PropertyInfo GetProperty(this LambdaExpression prop) =>
        (PropertyInfo)((MemberExpression)prop.Body).Member;

    public static string GetTypeName(this Type type) =>
        ClassMapping.GetDgraphType(type);
}

// copilot, generate a query builder to attend the above sample
public class DgraphQueryBuilder
{
    private readonly Request _request = new();

    public static async Task<Response> QueryAsync(IDgraph4NetClient client, Action<DgraphQueryBuilder> act)
    {
        var builder = new DgraphQueryBuilder();
        act(builder);
        await using var txn = (Txn)client.NewTransaction();
        return await txn.Do(builder._request);
    }

    public DgraphQueryBuilder Var(string name, Action<DgraphVarBuilder> act)
    {
        var builder = new DgraphVarBuilder(name);
        act(builder);
        _vars.Add(name, builder._var);
        return this;
    }

    public DgraphQueryBuilder From<T>(string alias, Action<DgraphFromBuilder<T>> act)
    {
        var builder = new DgraphFromBuilder<T>(alias);
        act(builder);
        _request.Query += builder._query;
        return this;
    }

    public DgraphQueryBuilder Aggregate(string alias, Action<DgraphAggregateBuilder> act)
    {
        var builder = new DgraphAggregateBuilder(alias);
        act(builder);
        _request.Query += builder._query;
        return this;
    }

    public class DgraphVarBuilder
    {
        internal readonly string _var;

        public DgraphVarBuilder(string name) => _var = name;
    }

    public class DgraphFromBuilder<T>
    {
        internal string _query;

        public DgraphFromBuilder(string alias) => _query = $"  {alias} as var(func: type({typeof(T).GetTypeName()})) {{\n";

        public DgraphFromBuilder<T> Filter(Action<DgraphFilterBuilder<T>> act)
        {
            var builder = new DgraphFilterBuilder<T>();
            act(builder);
            _query += builder._query;
            return this;
        }

        public DgraphFromBuilder<T> Select(Action<DgraphSelectBuilder<T>> act)
        {
            var builder = new DgraphSelectBuilder<T>();
            act(builder);
            _query += builder._query;
            return this;
        }
    }

    public class DgraphAggregateBuilder
    {
        internal string _query;

        public DgraphAggregateBuilder(string alias) => _query = $"  {alias}() {{\n";

        public DgraphAggregateBuilder Sum(string alias, Action<DgraphSumBuilder> act)
        {
            var builder = new DgraphSumBuilder(alias);

            act(builder);

            _query += builder._query;

            return this;
        }
    }

    public class DgraphFilterBuilder<T>
    {
        internal string _query;

        public DgraphFilterBuilder() => _query = "    @filter(";

        public DgraphFilterBuilder<T> Eq<TProp>(Expression<Func<T, TProp>> prop, TProp value)
        {
            var name = prop.GetPredicateName();
            _query += $"eq({name}, {value})";
            return this;
        }

        public DgraphFilterBuilder<T> Has<TProp>(Expression<Func<T, TProp>> prop)
        {
            var name = prop.GetPredicateName();
            _query += $"has({name})";
            return this;
        }

        public DgraphFilterBuilder<T> Gt<TProp>(Expression<Func<T, TProp>> prop, TProp value)
        {
            var name = prop.GetPredicateName();
            _query += $"gt({name}, {value})";
            return this;
        }

        public DgraphFilterBuilder<T> Count<TProp>(Expression<Func<T, TProp>> prop)
        {
            var name = prop.GetPredicateName();
            _query += $"count({name})";
            return this;
        }

        public DgraphFilterBuilder<T> UidVar(string name)
        {
            _vars.TryGetValue(name, out var uid);
            _query += $"uid({name})";
            return this;
        }
    }

    public class DgraphSelectBuilder<T>
    {
        internal string _query;

        public DgraphSelectBuilder() => _query = "    {\n";

        public DgraphSelectBuilder<T> Predicate<TProp>(Expression<Func<T, TProp>> prop)
        {
            var name = prop.GetPredicateName();
            _query += $"      {name}\n";
            return this;
        }

        public DgraphSelectBuilder<T> Alias(string alias, Action<DgraphAliasBuilder<T>> act)
        {
            var builder = new DgraphAliasBuilder<T>(alias);
            act(builder);
            _query += builder._query;
            return this;
        }
    }

    public class DgraphAliasBuilder<T>
    {
        internal string _query;

        public DgraphAliasBuilder(string alias) => _query = $"      {alias}: ";

        public DgraphAliasBuilder<T> Count(Expression<Func<T, object>> prop)
        {
            var name = prop.GetPredicateName();
            _query += $"count({name})";
            return this;
        }

        public DgraphAliasBuilder<T> Val(string value)
        {
            _query += value;
            return this;
        }
    }

    public class DgraphSumBuilder
    {
        internal string _query;

        public DgraphSumBuilder(string alias) => _query = $"    {alias}: sum(";

        public DgraphSumBuilder Val(string value)
        {
            _query += $"val({value})";
            return this;
        }
    }

    // copilot: generate a sample for this builder
    public static void Sample()
    {
        IDgraph4NetClient client = default;
        var response = client.Query(q => q
            .From<Person>("person", p => p
                .Filter(f => f
                    .Eq(p => p.Name, "Alice")
                    .Gt(p => p.Age, 18)
                    .Has(p => p.Friends)
                    .Count(p => p.Friends)
                    .UidVar("b")
                )
                .Select(s => s
                    .Predicate(p => p.Name)
                    .Alias("friends", f => f
                        .Count(p => p.Friends)
                    )
                )
            )
            .Aggregate("agg", b => b
                .Sum("sum", s => s
                    .Val("b")
                )
            )
        );
    }
}

public class Person
{
    public string Name { get; set; }
    public List<Person> Friends { get; set; }
    public int Age { get; set; }
}
