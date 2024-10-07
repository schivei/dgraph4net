using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Dgraph4Net.ActiveRecords;

namespace Dgraph4Net;

public static class DType<T>
{
    public static string Name { get; } =
        ClassMapping.ClassMappings.TryGetValue(typeof(T), out var classMap)
            ? classMap.DgraphType
            : typeof(T).Name;

    public static string Predicate<TProperty>(Expression<Func<T, TProperty>> expression)
    {
        if (expression.Body is not MemberExpression member)
            throw new ArgumentException($"Expression '{expression}' refers to a method, not a property.");

        if (member.Member is not PropertyInfo propInfo)
            throw new ArgumentException($"Expression '{expression}' refers to a field, not a property.");

        return ClassMap.Predicates.TryGetValue(propInfo, out var predicate)
            ? predicate.PredicateName
            : propInfo.Name;
    }

    public static string ExpandAll(int deep = 0)
    {
        if(!ClassMapping.ClassMappings.TryGetValue(typeof(T), out var classMap))
            throw new NotSupportedException($"The type '{typeof(T).FullName}' is not mapped.");

        var predicates = ClassMap.Predicates.Values
            .Where(p => p.ClassMap == classMap).ToList();

        var sb = new StringBuilder();

        foreach (var predicate in predicates)
        {
            if (predicate is IEdgePredicate ep && deep > 0)
            {
                sb.Append(ep.PredicateName).AppendLine(" {");
                var subType = typeof(DType<>).MakeGenericType(ep.EdgeType);
                var exp = subType.GetMethod(nameof(ExpandAll))?.Invoke(null, [deep - 1]);
                sb.Append(exp);
                sb.AppendLine("}");
            }
            else
            {
                sb.AppendLine(predicate.PredicateName);
            }
        }

        return sb.ToString();
    }
}
