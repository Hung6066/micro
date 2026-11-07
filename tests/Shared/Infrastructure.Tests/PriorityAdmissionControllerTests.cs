using FluentAssertions;
using His.Hope.Infrastructure.Qos;

namespace Infrastructure.Tests;

public sealed class PriorityAdmissionControllerTests
{
    [Fact]
    public async Task Reserves_capacity_for_high_priority_workloads()
    {
        var controller = new PriorityAdmissionController(new PriorityAdmissionOptions
        {
            MaxConcurrentRequests = 4,
            ReservedHighPriorityFraction = 0.25,
            QueueCapacity = 4,
            MaxWaitMilliseconds = 20
        });

        await using var first = await controller.AcquireAsync(3, CancellationToken.None);
        await using var second = await controller.AcquireAsync(3, CancellationToken.None);
        await using var third = await controller.AcquireAsync(3, CancellationToken.None);

        var fourth = await controller.AcquireAsync(3, CancellationToken.None);
        fourth.Should().BeNull();

        await using var high = await controller.AcquireAsync(0, CancellationToken.None);
        high.Should().NotBeNull();
    }

    [Fact]
    public async Task Aging_allows_an_old_low_priority_waiter_to_progress()
    {
        var controller = new PriorityAdmissionController(new PriorityAdmissionOptions
        {
            MaxConcurrentRequests = 1,
            ReservedHighPriorityFraction = 0,
            QueueCapacity = 4,
            MaxWaitMilliseconds = 500,
            AgingStepMilliseconds = 10
        });

        await using var active = (await controller.AcquireAsync(0, CancellationToken.None))!;
        var lowTask = controller.AcquireAsync(4, CancellationToken.None).AsTask();
        await Task.Delay(55);
        var highTask = controller.AcquireAsync(0, CancellationToken.None).AsTask();

        await active.DisposeAsync();
        (await Task.WhenAny(lowTask, highTask)).Should().Be(lowTask);

        await using var low = (await lowTask)!;
        low.Should().NotBeNull();
        await low!.DisposeAsync();

        await using var high = (await highTask)!;
        high.Should().NotBeNull();
    }
}
