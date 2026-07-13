using System.Linq.Expressions;

namespace His.Hope.SharedKernel.Specifications;

public abstract class Specification<T>
{
    public abstract Expression<Func<T, bool>> ToExpression();

    public bool IsSatisfiedBy(T entity)
    {
        var predicate = ToExpression().Compile();
        return predicate(entity);
    }

    public Specification<T> And(Specification<T> specification) =>
        new AndSpecification<T>(this, specification);

    public Specification<T> Or(Specification<T> specification) =>
        new OrSpecification<T>(this, specification);

    public Specification<T> Not() =>
        new NotSpecification<T>(this);
}

public class AndSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;

    public AndSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpr = _left.ToExpression();
        var rightExpr = _right.ToExpression();
        var param = leftExpr.Parameters[0];
        var body = Expression.AndAlso(leftExpr.Body, rightExpr.Body);
        return Expression.Lambda<Func<T, bool>>(body, param);
    }
}

public class OrSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;

    public OrSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpr = _left.ToExpression();
        var rightExpr = _right.ToExpression();
        var param = leftExpr.Parameters[0];
        var body = Expression.OrElse(leftExpr.Body, rightExpr.Body);
        return Expression.Lambda<Func<T, bool>>(body, param);
    }
}

public class NotSpecification<T> : Specification<T>
{
    private readonly Specification<T> _specification;

    public NotSpecification(Specification<T> specification)
    {
        _specification = specification;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var expr = _specification.ToExpression();
        var param = expr.Parameters[0];
        var body = Expression.Not(expr.Body);
        return Expression.Lambda<Func<T, bool>>(body, param);
    }
}
