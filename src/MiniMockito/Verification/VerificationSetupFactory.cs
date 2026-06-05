using System.Linq.Expressions;
using MiniMockito.Core;
using MiniMockito.Exceptions;
using MiniMockito.Matching;
using MiniMockito.Proxy.ClassProxy;

namespace MiniMockito.Verification;

internal static class VerificationSetupFactory
{
    internal static VerificationSetup Create(Expression expression)
    {
        var methodCall = Unwrap(expression) as MethodCallExpression
            ?? throw new VerificationException("Verify requires a direct method call expression.");

        if (methodCall.Object is null)
        {
            throw new VerificationException("Verify requires a method call on a MiniMockito mock.");
        }

        var mock = Evaluate(methodCall.Object)
            ?? throw new VerificationException("Verify could not evaluate the mock instance.");

        var state = MockRepository.Default.GetState(mock);
        ClassProxyValidation.EnsureMethodSupported(state.MockedType, methodCall.Method);
        var matchers = methodCall.Arguments
            .Select(CreateArgumentMatcher)
            .ToArray();

        return new VerificationSetup(state, new InvocationMatcher(methodCall.Method, matchers));
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

        if (TryCreateCaptorMatcher(expression, out var captorMatcher))
        {
            return captorMatcher;
        }

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
                    ?? throw new VerificationException("InRange minimum must implement IComparable.");
                var maximum = Evaluate(methodCall.Arguments[1]) as IComparable
                    ?? throw new VerificationException("InRange maximum must implement IComparable.");
                return new RangeArgumentMatcher(minimum, maximum);
            }
        }

        return new EqualityArgumentMatcher(Evaluate(expression));
    }

    private static bool TryCreateCaptorMatcher(Expression expression, out ArgumentMatcher matcher)
    {
        if (expression is MemberExpression memberExpression
            && memberExpression.Member.Name == nameof(ArgumentCaptor<object>.Value)
            && memberExpression.Expression is not null
            && Evaluate(memberExpression.Expression) is { } captor
            && TryGetCaptorValueType(captor.GetType(), out var capturedType))
        {
            matcher = (ArgumentMatcher)Activator.CreateInstance(
                typeof(CapturingArgumentMatcher<>).MakeGenericType(capturedType),
                captor)!;
            return true;
        }

        matcher = null!;
        return false;
    }

    private static bool TryGetCaptorValueType(Type type, out Type capturedType)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ArgumentCaptor<>))
        {
            capturedType = type.GetGenericArguments()[0];
            return true;
        }

        capturedType = typeof(object);
        return false;
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

        throw new VerificationException("Is matcher requires a predicate expression.");
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
