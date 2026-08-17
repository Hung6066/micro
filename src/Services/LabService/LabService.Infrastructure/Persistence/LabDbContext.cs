using System.Reflection;
using His.Hope.Authorization;
using His.Hope.Infrastructure.Outbox;
using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace His.Hope.LabService.Infrastructure.Persistence;

public class LabDbContext : DbContext, IUnitOfWork
{
    private readonly IMediator? _mediator;
    private readonly FacilityAccessScope _facilityScope;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public DbSet<LabOrder> LabOrders => Set<LabOrder>();
    public DbSet<CriticalAlertRule> CriticalAlertRules => Set<CriticalAlertRule>();
    public DbSet<CriticalAlert> CriticalAlerts => Set<CriticalAlert>();
    public DbSet<CriticalAlertAuditEntry> CriticalAlertAuditEntries => Set<CriticalAlertAuditEntry>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public LabDbContext(
        DbContextOptions<LabDbContext> options,
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
        modelBuilder.Entity<LabOrder>().HasQueryFilter(order =>
            !_facilityScope.IsEnforced || _facilityScope.IsCrossFacility ||
            _facilityScope.FacilityIds.Contains(order.FacilityId!));
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Type).HasColumnName("type").HasMaxLength(500).IsRequired();
            entity.Property(e => e.Content).HasColumnName("content").IsRequired();
            entity.Property(e => e.CorrelationId).HasColumnName("correlationid").HasMaxLength(200);
            entity.Property(e => e.CausationId).HasColumnName("causationid").HasMaxLength(200);
            entity.Property(e => e.OccurredOn).HasColumnName("occurredon").IsRequired();
            entity.Property(e => e.ProcessedOn).HasColumnName("processedon");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Error).HasColumnName("error").HasMaxLength(1000);
            entity.Property(e => e.RetryCount).HasColumnName("retrycount");
            entity.Property(e => e.LastRetryOn).HasColumnName("lastretryon");
            entity.Property(e => e.LockExpiresAt).HasColumnName("lockexpiresat");
            entity.Property(e => e.ClaimedBy).HasColumnName("claimed_by").HasMaxLength(100);
            entity.Property(e => e.NextAttemptAt).HasColumnName("next_attempt_at");
            entity.Property(e => e.DeadLetteredOn).HasColumnName("dead_lettered_on");
            entity.HasIndex(e => new { e.Status, e.OccurredOn });
        });
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        FacilityScopeEfExtensions.StampAddedFacilities(this, _facilityScope, _httpContextAccessor);
        var domainEvents = ChangeTracker.Entries<AggregateRoot<LabOrderId>>()
            .Select(e => e.Entity.DomainEvents)
            .SelectMany(e => e)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in domainEvents)
        {
            if (_mediator is not null)
                await _mediator.Publish(domainEvent, cancellationToken);
        }

        return result;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        FacilityScopeEfExtensions.StampAddedFacilities(this, _facilityScope, _httpContextAccessor);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }
}
