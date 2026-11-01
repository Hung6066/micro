using FluentAssertions;
using His.Hope.SharedKernel.Specifications;

namespace His.Hope.SharedKernel.Tests;

public sealed class SpecificationTests
{
    [Fact]
    public void And_composes_expressions_with_independent_parameters()
    {
        var specification = new MinimumValueSpecification(10)
            .And(new MaximumValueSpecification(20));

        var predicate = specification.ToExpression().Compile();

        predicate(10).Should().BeTrue();
        predicate(20).Should().BeTrue();
        predicate(9).Should().BeFalse();
        predicate(21).Should().BeFalse();
    }

    [Fact]
    public void Or_and_not_preserve_composed_predicate_semantics()
    {
        var specification = new MinimumValueSpecification(10)
            .Or(new MaximumValueSpecification(0))
            .Not();

        var predicate = specification.ToExpression().Compile();

        predicate(5).Should().BeTrue();
        predicate(10).Should().BeFalse();
        predicate(-1).Should().BeFalse();
    }

    private sealed class MinimumValueSpecification(int minimum) : Specification<int>
    {
        public override System.Linq.Expressions.Expression<Func<int, bool>> ToExpression() =>
            value => value >= minimum;
    }

    private sealed class MaximumValueSpecification(int maximum) : Specification<int>
    {
        public override System.Linq.Expressions.Expression<Func<int, bool>> ToExpression() =>
            value => value <= maximum;
    }
}
