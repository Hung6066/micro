using System.Reflection;
using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.Infrastructure.Outbox;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.BillingService.Infrastructure.Persistence;

public class BillingDbContext : DbContext, IUnitOfWork
{
    private readonly IMediator _mediator;

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public BillingDbContext(
        DbContextOptions<BillingDbContext> options,
        IMediator mediator)
        : base(options)
    {
        _mediator = mediator;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("billing");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
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
            entity.HasIndex(e => new { e.Status, e.OccurredOn });
        });
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
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
            await _mediator.Publish(domainEvent, cancellationToken);
        }

        return result;
    }
}
