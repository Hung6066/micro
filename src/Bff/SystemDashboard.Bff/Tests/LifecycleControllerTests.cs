using Microsoft.Extensions.Logging;
using NSubstitute;
using SystemDashboard.Bff.Aggregators;
using SystemDashboard.Bff.Services;
using Xunit;

namespace SystemDashboard.Bff.Tests;

public sealed class LifecycleControllerTests
{
    [Fact]
    public async Task StartAsync_PassesThroughToService()
    {
        // Arrange
        var service = Substitute.For<IServiceLifecycleService>();
        service.StartAsync("identity-service", default).Returns(true);

        var logger = Substitute.For<ILogger<LifecycleController>>();
        var controller = new LifecycleController(service, logger);

        // Act
        var result = await controller.StartAsync("identity-service", default);

        // Assert
        Assert.True(result);
        await service.Received(1).StartAsync("identity-service", default);
    }

    [Fact]
    public async Task StopAsync_PassesThroughToService()
    {
        // Arrange
        var service = Substitute.For<IServiceLifecycleService>();
        service.StopAsync("patient-service", default).Returns(true);

        var logger = Substitute.For<ILogger<LifecycleController>>();
        var controller = new LifecycleController(service, logger);

        // Act
        var result = await controller.StopAsync("patient-service", default);

        // Assert
        Assert.True(result);
        await service.Received(1).StopAsync("patient-service", default);
    }

    [Fact]
    public async Task RestartAsync_PassesThroughToService()
    {
        // Arrange
        var service = Substitute.For<IServiceLifecycleService>();
        service.RestartAsync("appointment-service", default).Returns(false);

        var logger = Substitute.For<ILogger<LifecycleController>>();
        var controller = new LifecycleController(service, logger);

        // Act
        var result = await controller.RestartAsync("appointment-service", default);

        // Assert
        Assert.False(result);
        await service.Received(1).RestartAsync("appointment-service", default);
    }
}
