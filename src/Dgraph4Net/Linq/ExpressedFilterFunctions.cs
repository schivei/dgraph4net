using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Dgraph4Net;

internal sealed class ExpressedFilterFunctions
{
    private readonly StringBuilder _stringBuilder = new();

    public VarTriples Variables { get; } = [];

    public override string ToString() => _stringBuilder.ToString()[1..^1];

    public void OpenParenthesis() => _stringBuilder.Append('(');

    public void CloseParenthesis() => _stringBuilder.Append(')');

    public void And() => _stringBuilder.Append(" and ");

    public void Or() => _stringBuilder.Append(" or ");

    public void Not() => _stringBuilder.Append(" not ");

    public bool Append(string expression)
    {
        _stringBuilder.Append(expression);

        return true;
    }

    public static ExpressedFilterFunctions Parse(Expression<Func<IFilterFunctions, bool>> expression)
    {
        var expressed = new ExpressedFilterFunctions();

        var functions = new FilterFunctions(expressed);

        var visitor = new FilterExpressionVisitor(functions, expressed);
        visitor.Visit(expression.Body);

        return expressed;
    }

    private class FilterExpressionVisitor(FilterFunctions functions, ExpressedFilterFunctions expressed) : ExpressionVisitor
    {
        private readonly FilterFunctions _functions = functions;
        private readonly ExpressedFilterFunctions _expressed = expressed;

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType == ExpressionType.AndAlso)
            {
                _expressed.OpenParenthesis();
                Visit(node.Left);
                _expressed.And();
                Visit(node.Right);
                _expressed.CloseParenthesis();
            }
            else if (node.NodeType == ExpressionType.OrElse)
            {
                _expressed.OpenParenthesis();
                Visit(node.Left);
                _expressed.Or();
                Visit(node.Right);
                _expressed.CloseParenthesis();
            }
            return node;
        }

        private static void ReadConstant(Expression expression, Type targetType, int position, ref object obj)
        {
            if (expression is ConstantExpression constant)
            {
                obj = Expression.Constant(constant.Value, targetType).Value;
            }
        }

        private static void ReadArray(Expression expression, Type targetType, int position, ref object obj)
        {
            if (expression is NewArrayExpression array)
            {
                obj = array.Expressions.Select(x => ((ConstantExpression)x).Value).ToArray();
            }
        }

        private static void ReadParameter(Expression expression, Type targetType, int position, ref object obj)
        {
            if (expression is ParameterExpression parameter)
            {
                obj = parameter.Name;
            }
        }

        private static void ReadMember(Expression expression, Type targetType, int position, ref object obj)
        {
            if (expression is MemberExpression member)
            {
                var memberName = member.Member.Name;
                var entityType = member.Expression.Type;

                if (!entityType.IsAssignableTo(typeof(IEntity)))
                    throw new ArgumentException("The entity must be an IEntity");

                obj = TypeExtensions.Predicate(entityType, memberName);
            }
        }

        private static void ReadLambda(Expression expression, Type targetType, int position, ref object obj)
        {
            if (expression is LambdaExpression lambda)
            {
                ReadMember(lambda.Body, targetType, position, ref obj);

                ReadUnary(lambda.Body, targetType, position, ref obj);

                ReadConstant(lambda.Body, targetType, position, ref obj);

                ReadArray(lambda.Body, targetType, position, ref obj);

                ReadMethodCall(lambda.Body, targetType, position, ref obj);

                ReadParameter(lambda.Body, targetType, position, ref obj);

                ReadLambda(lambda.Body, targetType, position, ref obj);
            }
        }

        private static void ReadMethodCall(Expression expression, Type targetType, int position, ref object obj)
        {
            if (expression is MethodCallExpression methodCall)
            {
                obj = methodCall.Method.Invoke(methodCall.Object, methodCall.Arguments.Select(x => ((ConstantExpression)x).Value).ToArray());
            }
        }

        private static void ReadUnary(Expression expression, Type targetType, int position, ref object obj)
        {
            if (expression is UnaryExpression unary)
            {
                ReadConstant(unary.Operand, targetType, position, ref obj);

                ReadArray(unary.Operand, targetType, position, ref obj);

                ReadMethodCall(unary.Operand, targetType, position, ref obj);

                ReadLambda(unary.Operand, targetType, position, ref obj);

                ReadMember(unary.Operand, targetType, position, ref obj);

                ReadUnary(unary.Operand, targetType, position, ref obj);

                ReadParameter(unary.Operand, targetType, position, ref obj);
            }
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Object is ParameterExpression && node.Method.ReturnType == typeof(bool))
            {
                var parameters = node.Arguments.ToArray();
                var methodArgs = node.Method.GetParameters();
                var margs = new object[parameters.Length];
                var method = node.Method;

                for (var i = 0; i < parameters.Length; i++)
                {
                    margs[i] = null;

                    var exp = Visit(parameters[i]);

                    ReadConstant(exp, methodArgs[i].ParameterType, i, ref margs[i]);

                    ReadArray(exp, methodArgs[i].ParameterType, i, ref margs[i]);

                    ReadParameter(exp, methodArgs[i].ParameterType, i, ref margs[i]);

                    ReadMethodCall(exp, methodArgs[i].ParameterType, i, ref margs[i]);

                    ReadMember(exp, methodArgs[i].ParameterType, i, ref margs[i]);

                    ReadLambda(exp, methodArgs[i].ParameterType, i, ref margs[i]);

                    ReadUnary(exp, methodArgs[i].ParameterType, i, ref margs[i]);

                    if (margs[i] is null)
                        throw new ArgumentException("Invalid parameter type");
                }

                method = method.DeclaringType.GetMethods()
                    .Where(x => x.Name == method.Name && !x.IsGenericMethod && x.GetParameters().Length == parameters.Length)
                    .FirstOrDefault(m =>
                        m.GetParameters() is ParameterInfo[] infos &&
                        margs.Where((v, i) => v is null || v?.GetType() == infos[i].ParameterType).Any());

                method.Invoke(_functions, margs);
                return node;
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType == ExpressionType.Not)
            {
                Visit(node.Operand);
                _expressed.Not();
            }
            return node;
        }
    }
}
