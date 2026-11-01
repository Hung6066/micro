using System.Reflection;
using His.Hope.Authorization;
using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.Entities;
using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.Infrastructure.Outbox;
using His.Hope.Infrastructure.DataLifecycle;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace His.Hope.ClinicalService.Infrastructure.Persistence;

public class ClinicalDbContext : DbContext, IUnitOfWork
{
    private readonly IMediator? _mediator;
    private readonly FacilityAccessScope _facilityScope;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<ClinicalNote> ClinicalNotes => Set<ClinicalNote>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public ClinicalDbContext(
        DbContextOptions<ClinicalDbContext> options,
        IMediator? mediator = null,
        IHttpContextAccessor? httpContextAccessor = null)
        : base(options)
    {
        _mediator = mediator;
        _httpContextAccessor = httpContextAccessor;
        _facilityScope = FacilityScopeEfExtensions.Resolve(httpContextAccessor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        modelBuilder.Entity<Encounter>().HasQueryFilter(encounter =>
            EF.Property<bool?>(encounter, "IsDeleted") != true &&
            (!_facilityScope.IsEnforced || _facilityScope.IsCrossFacility ||
            _facilityScope.FacilityIds.Contains(encounter.FacilityId!)));
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(e => e.Id);
            // The initial clinical migration created the outbox table with
            // quoted PascalCase names. Map the complete legacy shape
            // explicitly so EF does not emit lowercase SQL (for example,
            // o.id instead of "Id").
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Type).HasColumnName("type").HasMaxLength(500).IsRequired();
            entity.Property(e => e.Content).HasColumnName("content").IsRequired();
            entity.Property(e => e.CorrelationId).HasColumnName("correlation_id").HasMaxLength(200);
            entity.Property(e => e.CausationId).HasColumnName("causation_id").HasMaxLength(200);
            entity.Property(e => e.OccurredOn).HasColumnName("occurred_on").IsRequired();
            entity.Property(e => e.ProcessedOn).HasColumnName("processed_on");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Error).HasColumnName("error").HasMaxLength(1000);
            entity.Property(e => e.RetryCount).HasColumnName("retry_count");
            entity.Property(e => e.LastRetryOn).HasColumnName("last_retry_on");
            entity.Property(e => e.LockExpiresAt).HasColumnName("lock_expires_at");
            entity.Property(e => e.ClaimedBy).HasColumnName("claimed_by").HasMaxLength(100);
            entity.Property(e => e.NextAttemptAt).HasColumnName("next_attempt_at");
            entity.Property(e => e.DeadLetteredOn).HasColumnName("dead_lettered_on");
            entity.HasIndex(e => new { e.Status, e.OccurredOn }).HasDatabaseName("ix_outboxmessages_status_occurredon");
        });
        base.OnModelCreating(modelBuilder);
        HisHopeDataConventions.Apply(modelBuilder, typeof(Encounter));
    }

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        FacilityScopeEfExtensions.StampAddedFacilities(this, _facilityScope, _httpContextAccessor);
        var domainEvents = ChangeTracker.Entries<AggregateRoot<EncounterId>>()
            .Select(e => e.Entity.DomainEvents)
            .SelectMany(e => e)
            .ToList();

        // Set CreatedAt on all added entities that have a CreatedAt property
        // (handles both Entity<EncounterId> and any other entity types)
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
        {
            var createdAt = entry.Metadata.FindProperty("CreatedAt");
            if (createdAt is not null)
                entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        if (_mediator is not null)
        {
            foreach (var domainEvent in domainEvents)
            {
                await _mediator.Publish(domainEvent, cancellationToken);
            }
        }

        return result;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        FacilityScopeEfExtensions.StampAddedFacilities(this, _facilityScope, _httpContextAccessor);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }
}
