using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using NetGeo.Json;

namespace Dgraph4Net.ActiveRecords;

public abstract class ClassMap : IClassMap
{
    protected object _lock = new();

    public static ConcurrentDictionary<PropertyInfo, IPredicate> Predicates { get; } = new();

    public static ConcurrentDictionary<PropertyInfo, ConcurrentBag<IFacet>> Facets { get; } = new();

    public Type Type { get; protected internal set; }
    public string DgraphType { get; protected internal set; }

    /// <summary>
    /// Key is the property of the class, value is the property of the edge
    /// </summary>
    protected internal static ConcurrentDictionary<PropertyInfo, (PropertyInfo prop, IClassMap map)> PendingEdges { get; } = new();

    internal static IClassMap CreateInstance(Type type) =>
        (IClassMap)Activator.CreateInstance(type)!;

    public virtual void Start() { }
    public virtual void Finish() { }

    internal static bool TryGetType<TE>(out string dataType) =>
        TryGetType(typeof(TE), out dataType);

    internal static bool TryGetType(Type te, out string dataType)
    {
        switch (te)
        {
            case Type _ when te == typeof(Uid) ||
                             te.IsAssignableTo(typeof(IEntity)):
                dataType = "uid";
                break;
            case Type _ when te == typeof(string) ||
                             te == typeof(byte[]) ||
                             te == typeof(Guid):
                dataType = "string";
                break;
            case Type _ when te == typeof(short) ||
                             te == typeof(int) ||
                             te == typeof(long) ||
                             te == typeof(TimeOnly) ||
                             te == typeof(TimeSpan):
                dataType = "int";
                break;
            case Type _ when te == typeof(decimal) ||
                             te == typeof(double) ||
                             te == typeof(float):
                dataType = "float";
                break;
            case Type _ when te == typeof(DateTime) ||
                             te == typeof(DateTimeOffset) ||
                             te == typeof(DateOnly):
                dataType = "datetime";
                break;
            case Type _ when te.IsAssignableTo(typeof(GeoObject)):
                dataType = "geo";
                break;
            default:
                if (te.IsAssignableTo(typeof(IEnumerable)))
                {
                    var tp = te.BaseType == typeof(Array) ?
                        te.Assembly.GetType(te.FullName.Replace("[]", "")) :
                        Array.Find(te.GetInterfaces(), x => x.IsAssignableTo(typeof(IEnumerable<>)))?
                        .GetGenericArguments().FirstOrDefault();
                    if (tp is not null && TryGetType(tp, out var dt))
                        dataType = $"[{dt}]";
                    else
                        dataType = string.Empty;
                }
                else
                {
                    dataType = string.Empty;
                }

                return false;
        }

        return true;
    }

    internal static PropertyInfo GetProperty<T>(Expression expression)
    {
        var lambda = expression as LambdaExpression ??
            throw new ArgumentException("Invalid expression.", nameof(expression));

        MemberExpression? memberExpr = null;

        if (lambda.Body.NodeType == ExpressionType.Convert)
        {
            memberExpr = ((UnaryExpression)lambda.Body).Operand as MemberExpression;
        }
        else if (lambda.Body.NodeType == ExpressionType.MemberAccess)
        {
            memberExpr = lambda.Body as MemberExpression;
        }

        if (memberExpr == null)
            throw new ArgumentException("Invalid expression.", nameof(expression));

        var pi = memberExpr.Member as PropertyInfo;

        if (pi is not null)
            return pi;

        var parent = memberExpr.Member.DeclaringType;

        if (!parent.IsAssignableTo(typeof(T)))
            throw new ArgumentException("Invalid expression.", nameof(expression));

        return parent.GetProperty(pi.Name, BindingFlags.Public) ??
            throw new ArgumentException("Invalid expression.", nameof(expression));
    }

    internal static PropertyInfo GetProperty(Expression expression) =>
        GetProperty<T>(expression);
}
