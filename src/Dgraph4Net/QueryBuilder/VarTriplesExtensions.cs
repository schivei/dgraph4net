using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using Dgraph4Net.ActiveRecords;

namespace Dgraph4Net;

/**
 * Copilot, analize the codes below and generates the translation of the query
# DQL
query Q($name: string){
  var(func: type(Person)) @filter(eq(name, $name)) {
    per as uid
    friend @filter(NOT eq(name, $name)) {
      fper as uid
    }
  }
  v as var(func: type(Person)) @filter(uid(per)) {
    cnt as count(friend)
  }
  me(func: type(Person)) @filter(uid(per)) {
    name
    friend @filter(uid(fper)) {
      name
    }
    friends: val(cnt)
  }
  aggregate {
    totalFriends: sum(cnt)
  }
}

# C#
var name = "Michael";
var vars = new VarTriples();
vars.Add(new("$name", name));
var query = @$"
query Q({vars.ToQueryString()}) {{
  var(func: type({DType<T>.Name})) @filter(eq({DType<T>.Predicate(p => p.Name)}, $name)) {
    per as uid
    {DType<T>.Predicate(p => p.Friend)} @filter(NOT eq({DType<T>.Predicate(p => p.Name)}, $name)) {
      fper as uid
    }
  }
  v as var(func: {DType<T>.Name}) @filter(uid(per)) {
    cnt as count({DType<T>.Predicate(p => p.Friend)})
  }
  me(func: type({DType<T>.Name})) @filter(uid(per)) {
    {DType<T>.ExpandAll()}
    {DType<T>.Predicate(p => p.Friend)} @filter(uid(fper)) {
      {DType<T>.ExpandAll()}
    }
    friends: val(cnt)
  }
  aggregate {
    totalFriends: sum(cnt)
  }
}";
Api.Response response = await client
    .NewTransaction(true, bestEffort, cancellationToken)
    .QueryWithVars(query, vars.ToDictionary());
 */
public static class VarTriplesExtensions
{
    public static string ToQueryString(this VarTriples vars)
    {
        return vars.Aggregate(new StringBuilder(), (sb, tpl) =>
        {
            if (sb.Length > 0)
                sb.Append(',');
            sb.Append('$').Append(tpl.Name).Append(':').Append(" ").Append(tpl.TypeName);
            return sb;
        }, sb => sb.ToString());
    }
}
