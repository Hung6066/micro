using FluentAssertions;
using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.ValueObjects;
using His.Hope.LabService.Infrastructure.Persistence;
using His.Hope.LabService.Infrastructure.Persistence.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace His.Hope.LabService.Integration.Tests;

public class LabOrderRepositoryIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private LabDbContext _context = null!;
    private LabOrderRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("hishopetest")
            .WithUsername("testuser")
            .WithPassword("testpass123!")
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();

        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(LabOrderRepositoryIntegrationTests).Assembly));
        services.AddDbContext<LabDbContext>(options =>
            options.UseNpgsql(_container.GetConnectionString()));
        services.AddScoped<LabOrderRepository>();

        var provider = services.BuildServiceProvider();
        _context = provider.GetRequiredService<LabDbContext>();
        _repository = provider.GetRequiredService<LabOrderRepository>();

        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task AddAndRetrieve_ValidLabOrder_ReturnsLabOrderWithProperties()
    {
        var labOrder = LabOrder.Create(
            Guid.NewGuid(), Guid.NewGuid(), null,
            LabOrderPriority.Routine, "Routine blood work");

        var added = await _repository.AddAsync(labOrder);
        await _repository.UnitOfWork.SaveChangesAsync();

        var retrieved = await _repository.GetByIdAsync(labOrder.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(labOrder.Id);
        retrieved.Status.Code.Should().Be("PENDING");
        retrieved.Priority.Code.Should().Be("ROUTINE");
        retrieved.Notes.Should().Be("Routine blood work");
    }

    [Fact]
    public async Task AddLabOrder_WithTests_ReturnsOrderWithTests()
    {
        var labOrder = LabOrder.Create(
            Guid.NewGuid(), Guid.NewGuid(), null,
            LabOrderPriority.Urgent, null);

        var test1 = LabTest.Create(labOrder.Id, "CBC", "Complete Blood Count", "Blood");
        var test2 = LabTest.Create(labOrder.Id, "BMP", "Basic Metabolic Panel", "Blood");

        labOrder.AddTest(test1);
        labOrder.AddTest(test2);

        await _repository.AddAsync(labOrder);
        await _repository.UnitOfWork.SaveChangesAsync();

        var retrieved = await _repository.GetByIdAsync(labOrder.Id);

        retrieved.Should().NotBeNull();
        retrieved!.RequestedTests.Should().HaveCount(2);
        retrieved.RequestedTests.Should().Contain(t => t.TestCode == "CBC");
        retrieved.RequestedTests.Should().Contain(t => t.TestCode == "BMP");
    }

    [Fact]
    public async Task GetByPatient_ReturnsPatientLabOrders()
    {
        var patientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        var order1 = LabOrder.Create(patientId, providerId, null, LabOrderPriority.Routine, null);
        var order2 = LabOrder.Create(patientId, providerId, null, LabOrderPriority.Urgent, null);

        await _repository.AddAsync(order1);
        await _repository.AddAsync(order2);
        await _repository.UnitOfWork.SaveChangesAsync();

        var orders = await _repository.GetByPatientAsync(patientId);

        orders.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateLabOrder_AfterAddingTest_ReflectsChanges()
    {
        var labOrder = LabOrder.Create(
            Guid.NewGuid(), Guid.NewGuid(), null,
            LabOrderPriority.Routine, null);

        await _repository.AddAsync(labOrder);
        await _repository.UnitOfWork.SaveChangesAsync();

        var test = LabTest.Create(labOrder.Id, "LDH", "Lactate Dehydrogenase", "Blood");
        labOrder.AddTest(test);
        _repository.Update(labOrder);
        await _repository.UnitOfWork.SaveChangesAsync();

        var retrieved = await _repository.GetByIdAsync(labOrder.Id);
        retrieved.Should().NotBeNull();
        retrieved!.RequestedTests.Should().HaveCount(1);
        retrieved.RequestedTests.First().TestCode.Should().Be("LDH");
    }

    [Fact]
    public async Task RemoveLabOrder_DeletesFromDatabase()
    {
        var labOrder = LabOrder.Create(
            Guid.NewGuid(), Guid.NewGuid(), null,
            LabOrderPriority.Routine, null);

        await _repository.AddAsync(labOrder);
        await _repository.UnitOfWork.SaveChangesAsync();

        _repository.Remove(labOrder);
        await _repository.UnitOfWork.SaveChangesAsync();

        var retrieved = await _repository.GetByIdAsync(labOrder.Id);
        retrieved.Should().BeNull();
    }
}
