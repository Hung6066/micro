using FluentAssertions;
using His.Hope.IntegrationEvents.Patient;
using His.Hope.PatientService.Infrastructure.Projections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace His.Hope.PatientService.Integration.Tests;

public sealed class PatientProjectorIdempotencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("hishopetest")
        .WithUsername("testuser")
        .WithPassword("testpass123!")
        .WithCleanUp(true)
        .Build();

    private PatientReadDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var options = new DbContextOptionsBuilder<PatientReadDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        _db = new PatientReadDbContext(options);
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Replaying_the_same_registered_event_does_not_duplicate_the_projection()
    {
        var projector = new PatientProjector(_db, NullLogger<PatientProjector>.Instance);
        var @event = new PatientRegisteredIntegrationEvent(
            Guid.NewGuid(), "Jane Doe", "+66000000000", "F",
            new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc), "facility-1");

        await projector.HandleAsync(@event);
        await projector.HandleAsync(@event);

        (await _db.PatientProjections.CountAsync()).Should().Be(1);
    }
}
