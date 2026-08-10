using His.Hope.Bff.Core.Aggregation;
using Xunit;

namespace His.Hope.Bff.Core.Tests.Aggregation;

public class ParallelAggregationExecutorTests
{
    [Fact]
    public async Task AllTasksSucceed_ReturnsAllSuccesses_NoFailures()
    {
        var result = await ParallelAggregationExecutor.RunAsync(new()
        {
            ["a"] = () => Task.FromResult<object>("value-a"),
            ["b"] = () => Task.FromResult<object>(42),
        });

        Assert.Equal(2, result.Successes.Count);
        Assert.Empty(result.Failures);
        Assert.Equal("value-a", result.Successes["a"]);
    }

    [Fact]
    public async Task PartialFailure_ReturnsSuccessesAndFailures()
    {
        var result = await ParallelAggregationExecutor.RunAsync(new()
        {
            ["a"] = () => Task.FromResult<object>("ok"),
            ["b"] = () => throw new TimeoutException("Service timeout"),
        });

        Assert.Single(result.Successes);
        Assert.Single(result.Failures);
        Assert.Equal("b", result.Failures[0].Field);
        Assert.Contains("timeout", result.Failures[0].Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AllFail_ReturnsNoSuccesses()
    {
        var result = await ParallelAggregationExecutor.RunAsync(new()
        {
            ["a"] = () => throw new InvalidOperationException("err"),
            ["b"] = () => throw new InvalidOperationException("err"),
        });

        Assert.Empty(result.Successes);
        Assert.Equal(2, result.Failures.Length);
    }
}
