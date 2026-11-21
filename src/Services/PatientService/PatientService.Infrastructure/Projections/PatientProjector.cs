using His.Hope.EventBus.Abstractions;
using His.Hope.IntegrationEvents.Patient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace His.Hope.PatientService.Infrastructure.Projections;

/// <summary>
/// Projects patient integration events into the denormalized read model.
/// Implements the CQRS event-sourced projection pattern — consuming events
/// from the write side and updating the read-optimized <see cref="PatientProjection"/> table.
/// </summary>
public class PatientProjector :
    IIntegrationEventHandler<PatientRegisteredIntegrationEvent>,
    IIntegrationEventHandler<PatientUpdatedIntegrationEvent>
{
    private const string ProjectionName = nameof(PatientProjector);
    private readonly PatientReadDbContext _readDbContext;
    private readonly ILogger<PatientProjector> _logger;

    public PatientProjector(
        PatientReadDbContext readDbContext,
        ILogger<PatientProjector> logger)
    {
        _readDbContext = readDbContext;
        _logger = logger;
    }

    public async Task HandleAsync(
        PatientRegisteredIntegrationEvent @event,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _readDbContext.Database.BeginTransactionAsync(cancellationToken);
        if (await AlreadyProcessedAsync(@event.Id, cancellationToken))
            return;

        _logger.LogInformation(
            "Projecting PatientRegistered event {EventId} for patient {PatientId}",
            @event.Id, @event.PatientId);

        var projection = await _readDbContext.PatientProjections
            .AsTracking()
            .SingleOrDefaultAsync(x => x.PatientId == @event.PatientId, cancellationToken);
        if (projection is null)
        {
            projection = new PatientProjection
            {
                PatientId = @event.PatientId,
                FacilityId = @event.FacilityId,
                FullName = @event.FullName,
                DateOfBirth = @event.DateOfBirth,
                Gender = @event.GenderCode,
                PrimaryDiagnosis = null,
                LastVisitDate = null,
                EncounterCount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };
            _readDbContext.PatientProjections.Add(projection);
        }

        AddProcessedEvent(@event.Id);

        await _readDbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Successfully projected PatientRegistered event {EventId} for patient {PatientId}",
            @event.Id, @event.PatientId);
    }

    public async Task HandleAsync(
        PatientUpdatedIntegrationEvent @event,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _readDbContext.Database.BeginTransactionAsync(cancellationToken);
        if (await AlreadyProcessedAsync(@event.Id, cancellationToken))
            return;

        _logger.LogInformation(
            "Projecting PatientUpdated event {EventId} for patient {PatientId}",
            @event.Id, @event.PatientId);

        var existing = await _readDbContext.PatientProjections
            .AsTracking()
            .FirstOrDefaultAsync(p => p.PatientId == @event.PatientId, cancellationToken);

        if (existing is null)
        {
            _logger.LogWarning(
                "No read model found for patient {PatientId} during update projection. " +
                "Creating new projection from event data.",
                @event.PatientId);

            var projection = new PatientProjection
            {
                PatientId = @event.PatientId,
                FacilityId = @event.FacilityId,
                FullName = @event.FullName,
                DateOfBirth = default,
                Gender = string.Empty,
                PrimaryDiagnosis = null,
                LastVisitDate = null,
                EncounterCount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _readDbContext.PatientProjections.Add(projection);
        }
        else
        {
            existing.FullName = @event.FullName;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _readDbContext.SaveChangesAsync(cancellationToken);
        AddProcessedEvent(@event.Id);
        await _readDbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Successfully projected PatientUpdated event {EventId} for patient {PatientId}",
            @event.Id, @event.PatientId);
    }

    private Task<bool> AlreadyProcessedAsync(Guid eventId, CancellationToken cancellationToken) =>
        _readDbContext.ProcessedProjectionEvents.AnyAsync(
            x => x.EventId == eventId && x.ProjectionName == ProjectionName,
            cancellationToken);

    private void AddProcessedEvent(Guid eventId) =>
        _readDbContext.ProcessedProjectionEvents.Add(new ProcessedProjectionEvent
        {
            EventId = eventId,
            ProjectionName = ProjectionName,
            ProcessedAt = DateTimeOffset.UtcNow
        });
}
