using System.Reflection;
using His.Hope.Infrastructure.Outbox;
using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.LabService.Infrastructure.Persistence;

public class LabDbContext : DbContext, IUnitOfWork
{
    private readonly IMediator _mediator;

    public DbSet<LabOrder> LabOrders => Set<LabOrder>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public LabDbContext(
        DbContextOptions<LabDbContext> options,
        IMediator mediator)
        : base(options)
    {
        _mediator = mediator;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("lab");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(200);
            entity.Property(e => e.CausationId).HasMaxLength(200);
            entity.Property(e => e.OccurredOn).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Error).HasMaxLength(1000);
            entity.Property(e => e.LockExpiresAt);
            entity.HasIndex(e => new { e.Status, e.OccurredOn });
        });
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        var domainEvents = ChangeTracker.Entries<AggregateRoot<LabOrderId>>()
            .Select(e => e.Entity.DomainEvents)
            .SelectMany(e => e)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }

        return result;
    }
}
