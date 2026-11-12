using System.Linq.Expressions;

namespace Apex.Hrms.Application;

/// <summary>Composes predicate expression trees so they remain translatable to SQL.</summary>
public static class Predicates
{
    public static Expression<Func<T, bool>> Always<T>() => _ => true;

    public static Expression<Func<T, bool>> Never<T>() => _ => false;

    public static Expression<Func<T, bool>> And<T>(
        this Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right) =>
        Combine(left, right, Expression.AndAlso);

    public static Expression<Func<T, bool>> Or<T>(
        this Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right) =>
        Combine(left, right, Expression.OrElse);

    /// <summary>Matches rows where any of the string fields contains the term (database collation decides case).</summary>
    public static Expression<Func<T, bool>> ContainsAny<T>(
        string term,
        params Expression<Func<T, string?>>[] fields)
    {
        // A closure keeps the term a query parameter instead of an inlined SQL literal.
        Expression<Func<string>> termAccess = () => term;
        var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        var parameter = Expression.Parameter(typeof(T), "row");

        Expression? body = null;
        foreach (var field in fields)
        {
            var value = new ParameterReplacer(field.Parameters[0], parameter).Visit(field.Body);
            var match = Expression.Call(
                Expression.Coalesce(value, Expression.Constant(string.Empty)),
                contains,
                termAccess.Body);
            body = body is null ? match : Expression.OrElse(body, match);
        }

        return Expression.Lambda<Func<T, bool>>(body ?? Expression.Constant(true), parameter);
    }

    private static Expression<Func<T, bool>> Combine<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        Func<Expression, Expression, BinaryExpression> join)
    {
        var parameter = Expression.Parameter(typeof(T), "row");
        var leftBody = new ParameterReplacer(left.Parameters[0], parameter).Visit(left.Body);
        var rightBody = new ParameterReplacer(right.Parameters[0], parameter).Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(join(leftBody, rightBody), parameter);
    }

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}
