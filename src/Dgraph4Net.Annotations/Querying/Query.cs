using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Dgraph4Net.Annotations.Querying
{
    internal static class Exp
    {
        public static PropertyInfo GetProperty<T, TE>(this Expression<Func<T, TE>> expression) where T : class, IEntity, new()
        {
            if (!(expression.Body is MemberExpression member))
                throw new InvalidCastException($"Expression {expression} is not a member expression.");

            return typeof(T).GetProperty(member.Member.Name);
        }
        
        public static string GetPredicate<T, TE>(this Expression<Func<T, TE>> expression) where T : class, IEntity, new()
        {
            var prop = expression.GetProperty();

            return prop.Name.NormalizeColumnName<T>();
        }
    }

    public sealed class Query<T> where T : class, IEntity, new()
    {
        private readonly Dictionary<string, string> _vars;
        private readonly Filter<T> _filter;

        internal Query()
        {
            _vars = new Dictionary<string, string>();
            _filter = new Filter<T>();
        }

        public Query<T> UseVars(Dictionary<string, string> vars)
        {
            foreach (var (key, value) in vars)
            {
                if(_vars.ContainsKey(key))
                    _vars.Add(key, value);

                _vars[key] = value;
            }

            return this;
        }

        public Query<T> UseVars<TE>(params (Expression<Func<T, TE>> exp, TE value)[] vars)
        {
            if (vars.Length == 0) return this;

            foreach (var (exp, value) in vars)
            {
                var prop = exp.GetProperty();

                if(value is null)
                    throw new ArgumentNullException(prop.Name);

                var key = $"${prop.Name}";
                var val = value.ToString();

                if (!_vars.ContainsKey(key))
                    _vars.Add(key, val);

                _vars[key] = val;
            }

            return this;
        }

        public Query<T> Filter(Action<Filter<T>> filters)
        {
            filters(_filter);

            return this;
        }

        public Query<T> AddVarQuery(string alias) =>
            this;

        public Query<T> AddVarQuery() =>
            this;
    }

    public sealed class Filter<T> where T : class, IEntity, new()
    {
        private bool _cascade;

        internal Filter()
        {

        }

        public Filter<T> Eq(string predicate, object value)
        {
            return this;
        }

        public Filter<T> Eq<TE>(Expression<Func<T, TE>> field, TE value) =>
            Eq(field.GetPredicate(), value);

        public Filter<T> Eq<TE>(Expression<Func<T, TE>> field, Filter<T> value) =>
            Eq(field.GetPredicate(), value);

        public Filter<T> And(params Action<Filter<T>>[] filters) =>
            this;
        
        public Filter<T> Or(params Action<Filter<T>>[] filters) =>
            this;
        
        public Filter<T> Not(Action<Filter<T>> filter) =>
            this;
        
        public Filter<T> Has<TE>(Expression<Func<T, TE>> filter) =>
            this;

        internal string Build()
        {
            return string.Empty;
        }
    }
}
