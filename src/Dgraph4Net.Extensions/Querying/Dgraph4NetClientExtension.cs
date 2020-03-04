// ReSharper disable once CheckNamespace

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace Dgraph4Net
{
    public static class Dgraph4NetClientExtension
    {
        public static void Mutate(this Dgraph4NetClient client) { }

        public static void Query<T>(this Dgraph4NetClient client, Expression<Func<T, object>> exp,
            Dictionary<string, string> variables)
        {
            var e = exp.Body as MemberExpression;
            var att = typeof(T).GetProperty(e.Member.Name).GetCustomAttribute<Newtonsoft.Json.JsonPropertyAttribute>();
        }
    }

    public class DgraphQueryFactory { }

    public class DgraphMutationFactory { }
}
