using System.Linq.Expressions;
using Dgraph4Net.ActiveRecords;

namespace Dgraph4Net;

public static class TypeExtensions
{
    public static string DgraphType(this Type type)
    {
        if (ClassMapping.ClassMappings.TryGetValue(type, out var classMap))
            return classMap.DgraphType;

        return type.Name;
    }

    public static string DgraphType<T>(this T _) where T : IEntity =>
        DType<T>.Name;

    public static string Predicate(this Type type, string propertyName)
    {
        if (ClassMapping.ClassMappings.TryGetValue(type, out var classMap))
        {
            var preds = ClassMap.Predicates.Where(x => x.Value.ClassMap == classMap && x.Key.Name == propertyName).Select(x => x.Value);

            if (preds.Any())
                return preds.First().PredicateName;
        }

        return propertyName;
    }

    public static string Predicate<T, TProperty>(this T _, Expression<Func<T, TProperty>> expression) where T : IEntity =>
        DType<T>.Predicate(expression);
}
