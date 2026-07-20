using FluentAssertions;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Domain.ValueObjects;
using His.Hope.LabService.Infrastructure.Persistence;
using His.Hope.LabService.Infrastructure.Persistence.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace His.Hope.LabService.Integration.Tests;

public class CriticalAlertPersistenceIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private LabDbContext _context = null!;
    private ICriticalAlertRepository _alertRepository = null!;

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
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CriticalAlertPersistenceIntegrationTests).Assembly));
        services.AddDbContext<LabDbContext>(options =>
            options.UseNpgsql(_container.GetConnectionString()));
        services.AddScoped<ICriticalAlertRepository, CriticalAlertRepository>();

        var provider = services.BuildServiceProvider();
        _context = provider.GetRequiredService<LabDbContext>();
        _alertRepository = provider.GetRequiredService<ICriticalAlertRepository>();

        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task EnsureCreated_ShouldCreateAlertTables_AndPersistAlertGraph()
    {
        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        await connection.OpenAsync();

        foreach (var tableName in new[] { "CriticalAlertRules", "CriticalAlerts", "CriticalAlertAuditEntries" })
        {
            await using var command = new NpgsqlCommand(
                "select count(*) from information_schema.tables where table_schema = 'public' and table_name = @name",
                connection);
            command.Parameters.AddWithValue("name", tableName);

            var exists = (long)(await command.ExecuteScalarAsync())!;
            exists.Should().Be(1, $"{tableName} should be created by EnsureCreated");
        }

        var rule = CriticalAlertRule.Create(
            "CBC",
            "Complete Blood Count",
            "x10^9/L",
            null,
            10.0m,
            "user-1",
            "Dr. Jones");

        var alert = CriticalAlert.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            rule.Id,
            CriticalAlertTriggerType.Threshold,
            "CBC result 12.5 breached critical 10",
            "12.5",
            "x10^9/L",
            10.0m,
            "user-1",
            "Dr. Jones");

        _context.CriticalAlertRules.Add(rule);
        _context.CriticalAlerts.Add(alert);
        await _context.SaveChangesAsync();

        var persisted = await _context.CriticalAlerts
            .Include(x => x.AuditEntries)
            .SingleAsync(x => x.Id == alert.Id);

        persisted.AuditEntries.Should().ContainSingle(entry => entry.Action == "Created");
        persisted.Status.Should().Be(CriticalAlertStatus.Open);
    }

    [Fact]
    public async Task AddAndSaveAsync_WhenDuplicateCurrentAlertExists_ReturnsExistingAlertWithoutDuplication()
    {
        var orderId = Guid.NewGuid();
        var testId = Guid.NewGuid();

        var firstAlert = CriticalAlert.Create(
            orderId,
            testId,
            Guid.NewGuid(),
            null,
            CriticalAlertTriggerType.CriticalFlag,
            "Critical flag CRITICAL_HIGH",
            "18.5",
            "x10^9/L",
            null,
            "user-1",
            "Dr. Jones");

        var secondAlert = CriticalAlert.Create(
            orderId,
            testId,
            Guid.NewGuid(),
            null,
            CriticalAlertTriggerType.CriticalFlag,
            "Critical flag CRITICAL_HIGH",
            "19.0",
            "x10^9/L",
            null,
            "user-1",
            "Dr. Jones");

        var created = await _alertRepository.AddAndSaveAsync(firstAlert, orderId, testId);
        var duplicateResult = await _alertRepository.AddAndSaveAsync(secondAlert, orderId, testId);

        created.Should().NotBeNull();
        duplicateResult.Should().NotBeNull();
        duplicateResult!.Id.Should().Be(created!.Id);
        duplicateResult.AuditEntries.Should().ContainSingle(entry => entry.Action == "Created");

        (await _context.CriticalAlerts.CountAsync(a => a.LabOrderId == orderId && a.LabTestId == testId)).Should().Be(1);
    }
}
