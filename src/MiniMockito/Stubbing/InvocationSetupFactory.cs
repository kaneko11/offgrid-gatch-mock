using System.Linq.Expressions;
using MiniMockito.Core;
using MiniMockito.Exceptions;
using MiniMockito.Matching;
using MiniMockito.Proxy.ClassProxy;

namespace MiniMockito.Stubbing;

internal static class InvocationSetupFactory
{
    internal static StubSetup Create(Expression expression)
    {
        var methodCall = Unwrap(expression) as MethodCallExpression
            ?? throw new StubbingException("When requires a direct method call expression.");

        if (methodCall.Object is null)
        {
            throw new StubbingException("When requires a method call on a MiniMockito mock.");
        }

        var mock = Evaluate(methodCall.Object)
            ?? throw new StubbingException("When could not evaluate the mock instance.");

        var state = MockRepository.Default.GetState(mock);
        ClassProxyValidation.EnsureMethodSupported(state.MockedType, methodCall.Method);
        var matchers = methodCall.Arguments
            .Select(CreateArgumentMatcher)
            .ToArray();

        return new StubSetup(state, new InvocationMatcher(methodCall.Method, matchers), methodCall.Method.ReturnType);
    }

    private static Expression Unwrap(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            expression = unary.Operand;
        }

        return expression;
    }

    private static ArgumentMatcher CreateArgumentMatcher(Expression expression)
    {
        expression = Unwrap(expression);

        if (expression is MethodCallExpression methodCall && methodCall.Method.DeclaringType == typeof(Mock))
        {
            if (methodCall.Method.Name == nameof(Mock.Any))
            {
                return new AnyArgumentMatcher();
            }

            if (methodCall.Method.Name == nameof(Mock.Eq))
            {
                return new EqualityArgumentMatcher(Evaluate(methodCall.Arguments[0]));
            }

            if (methodCall.Method.Name == nameof(Mock.Is))
            {
                var predicateExpression = Evaluate(methodCall.Arguments[0]);
                var predicate = CompilePredicate(predicateExpression);
                return new PredicateArgumentMatcher(methodCall.Method.GetGenericArguments()[0], predicate);
            }

            if (methodCall.Method.Name == nameof(Mock.Null))
            {
                return new NullArgumentMatcher();
            }

            if (methodCall.Method.Name == nameof(Mock.NotNull))
            {
                return new NotNullArgumentMatcher();
            }

            if (methodCall.Method.Name == nameof(Mock.InRange))
            {
                var minimum = Evaluate(methodCall.Arguments[0]) as IComparable
                    ?? throw new StubbingException("InRange minimum must implement IComparable.");
                var maximum = Evaluate(methodCall.Arguments[1]) as IComparable
                    ?? throw new StubbingException("InRange maximum must implement IComparable.");
                return new RangeArgumentMatcher(minimum, maximum);
            }
        }

        return new EqualityArgumentMatcher(Evaluate(expression));
    }

    private static Delegate CompilePredicate(object? predicateExpression)
    {
        if (predicateExpression is LambdaExpression lambda)
        {
            return lambda.Compile();
        }

        if (predicateExpression is Delegate predicate)
        {
            return predicate;
        }

        throw new StubbingException("Is matcher requires a predicate expression.");
    }

    private static object? Evaluate(Expression expression)
    {
        if (expression is ConstantExpression constant)
        {
            return constant.Value;
        }

        var boxedExpression = Expression.Convert(expression, typeof(object));
        return Expression.Lambda<Func<object?>>(boxedExpression).Compile().Invoke();
    }
}
