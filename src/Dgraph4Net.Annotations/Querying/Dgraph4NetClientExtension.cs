// ReSharper disable once CheckNamespace

using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using Dgraph4Net.Annotations;

namespace Dgraph4Net
{
    public static class Dgraph4NetClientExtension
    {
        public static DgraphQuery ToQuery(this Dgraph4NetClient client) =>
            new DgraphQuery(client);

        public static void Mutate(this Dgraph4NetClient client) { }

        public static void Query<T>(this Dgraph4NetClient client, Expression<Func<T, object>> exp,
            Dictionary<string, string> variables)
        {
            var e = exp.Body as MemberExpression;
            var att = typeof(T).GetProperty(e.Member.Name).GetCustomAttribute<Newtonsoft.Json.JsonPropertyAttribute>();
        }
    }

    public class DgraphQuery
    {
        private readonly Dgraph4NetClient _client;
        private Dictionary<string, string> _vars;

        public DgraphQuery(Dgraph4NetClient client)
        {
            _client = client;
        }

        public DgraphQuery From<T>(Dictionary<string, string> vars = null) where T : class, new()
        {
            if (typeof(T).GetCustomAttribute<DgraphTypeAttribute>() is null)
                throw new InvalidConstraintException($"{typeof(T).Name} is not valid");

            _vars = vars ?? new Dictionary<string, string>();

            return null;
        }

        public DgraphQuery From<T, TE>(params (Expression<Func<T, TE>> exp, TE value)[] vars) where T : class, new()
        {
            if (typeof(T).GetCustomAttribute<DgraphTypeAttribute>() is null)
                throw new InvalidConstraintException($"{typeof(T).Name} is not valid");

            _vars = new Dictionary<string, string>();

            if (vars.Length > 0)
            {
                foreach (var (exp, value) in vars)
                {
                    var member = exp.Body as MemberExpression;
                    if (member is null)
                        throw new InvalidCastException($"Expression {exp} is not a member expression.");

                    var key = typeof(T).GetProperty(member.Member.Name);
                }
            }
            return null;
        }
    }

    public class DgraphMutationFactory { }
}
