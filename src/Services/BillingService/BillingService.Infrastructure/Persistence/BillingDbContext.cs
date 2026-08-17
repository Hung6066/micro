using System.Reflection;
using His.Hope.Authorization;
using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.Infrastructure.Outbox;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace His.Hope.BillingService.Infrastructure.Persistence;

public class BillingDbContext : DbContext, IUnitOfWork
{
    private readonly IMediator? _mediator;
    private readonly FacilityAccessScope _facilityScope;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public BillingDbContext(
        DbContextOptions<BillingDbContext> options,
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
        modelBuilder.HasDefaultSchema("billing");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        modelBuilder.Entity<Invoice>().HasQueryFilter(invoice =>
            !_facilityScope.IsEnforced || _facilityScope.IsCrossFacility ||
            _facilityScope.FacilityIds.Contains(invoice.FacilityId!));
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Type).HasColumnName("Type").HasMaxLength(500).IsRequired();
            entity.Property(e => e.Content).HasColumnName("Content").IsRequired();
            entity.Property(e => e.CorrelationId).HasColumnName("CorrelationId").HasMaxLength(200);
            entity.Property(e => e.CausationId).HasColumnName("CausationId").HasMaxLength(200);
            entity.Property(e => e.OccurredOn).HasColumnName("OccurredOn").IsRequired();
            entity.Property(e => e.ProcessedOn).HasColumnName("ProcessedOn");
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Error).HasColumnName("Error").HasMaxLength(1000);
            entity.Property(e => e.RetryCount).HasColumnName("RetryCount");
            entity.Property(e => e.LastRetryOn).HasColumnName("LastRetryOn");
            entity.Property(e => e.LockExpiresAt).HasColumnName("LockExpiresAt");
            entity.Property(e => e.ClaimedBy).HasColumnName("ClaimedBy").HasMaxLength(100);
            entity.Property(e => e.NextAttemptAt).HasColumnName("NextAttemptAt");
            entity.Property(e => e.DeadLetteredOn).HasColumnName("DeadLetteredOn");
            entity.HasIndex(e => new { e.Status, e.OccurredOn });
        });
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        FacilityScopeEfExtensions.StampAddedFacilities(this, _facilityScope, _httpContextAccessor);
        var domainEvents = ChangeTracker.Entries<AggregateRoot<InvoiceId>>()
            .Select(e => e.Entity.DomainEvents)
            .SelectMany(e => e)
            .ToList();

        // Set CreatedAt on all added entities that have a CreatedAt property
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
        {
            var createdAt = entry.Metadata.FindProperty("CreatedAt");
            if (createdAt is not null)
                entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
        }

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
