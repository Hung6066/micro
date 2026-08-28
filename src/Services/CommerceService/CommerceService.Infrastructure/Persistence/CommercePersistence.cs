using System.Text.Json;
using His.Hope.CommerceService.Application.Orders;
using His.Hope.Contracts.Commerce;
using His.Hope.Persistence;
using His.Hope.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.CommerceService.Infrastructure.Persistence;

public sealed class CommerceDbContext(DbContextOptions<CommerceDbContext> options) : DbContext(options)
{
    public DbSet<CommerceOrderEntity> Orders => Set<CommerceOrderEntity>();
    public DbSet<CommerceProductEntity> Products => Set<CommerceProductEntity>();
    public DbSet<CommerceOrderLineEntity> OrderLines => Set<CommerceOrderLineEntity>();
    public DbSet<CommerceCartEntity> Carts => Set<CommerceCartEntity>();
    public DbSet<CommerceCartLineEntity> CartLines => Set<CommerceCartLineEntity>();
    public DbSet<CommerceProfileEntity> Profiles => Set<CommerceProfileEntity>();
    public DbSet<CommerceNotificationEntity> Notifications => Set<CommerceNotificationEntity>();
    public DbSet<CommerceRfqEntity> Rfqs => Set<CommerceRfqEntity>();
    public DbSet<CommerceRfqLineEntity> RfqLines => Set<CommerceRfqLineEntity>();
    public DbSet<CommerceOutboxMessageEntity> OutboxMessages => Set<CommerceOutboxMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CommerceOrderEntity>(entity =>
        {
            entity.ToTable("commerce_orders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.BuyerUserId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.TotalAmount).HasPrecision(18, 4);
            entity.HasIndex(x => new { x.TenantKey, x.CreatedAt });
            entity.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CommerceProductEntity>(entity =>
        {
            entity.ToTable("commerce_products");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Sku).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(18, 4);
            entity.Property(x => x.WholesaleUnitPrice).HasPrecision(18, 4);
            entity.HasIndex(x => new { x.TenantKey, x.Sku }).IsUnique();
        });

        modelBuilder.Entity<CommerceOrderLineEntity>(entity =>
        {
            entity.ToTable("commerce_order_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Sku).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(500).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(18, 4);
            entity.HasIndex(x => new { x.OrderId, x.Sku });
        });

        modelBuilder.Entity<CommerceCartEntity>(entity =>
        {
            entity.ToTable("commerce_carts");
            entity.HasKey(x => new { x.TenantKey, x.UserId });
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UserId).HasMaxLength(200).IsRequired();
            entity.HasMany(x => x.Lines).WithOne().HasForeignKey(x => new { x.TenantKey, x.UserId }).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CommerceCartLineEntity>(entity =>
        {
            entity.ToTable("commerce_cart_lines");
            entity.HasKey(x => new { x.TenantKey, x.UserId, x.ProductId });
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UserId).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.UserId });
        });

        modelBuilder.Entity<CommerceProfileEntity>(entity =>
        {
            entity.ToTable("commerce_profiles");
            entity.HasKey(x => new { x.TenantKey, x.UserId });
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UserId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(50).IsRequired();
            entity.Property(x => x.CompanyName).HasMaxLength(300).IsRequired();
            entity.Property(x => x.PriceTier).HasMaxLength(30).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<CommerceNotificationEntity>(entity =>
        {
            entity.ToTable("commerce_notifications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UserId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.UserId, x.CreatedAt });
        });

        modelBuilder.Entity<CommerceRfqEntity>(entity =>
        {
            entity.ToTable("commerce_rfqs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.BuyerUserId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.QuotedTotal).HasPrecision(18, 4);
            entity.Property(x => x.OperatorNotes).HasMaxLength(4000);
            entity.HasIndex(x => new { x.TenantKey, x.BuyerUserId, x.CreatedAt });
            entity.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CommerceRfqLineEntity>(entity =>
        {
            entity.ToTable("commerce_rfq_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => new { x.RfqId, x.ProductId });
        });

        modelBuilder.Entity<CommerceOutboxMessageEntity>(entity =>
        {
            entity.ToTable("commerce_outbox_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Content).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Error).HasMaxLength(2000);
            entity.HasIndex(x => new { x.Status, x.OccurredAt });
        });
    }
}

public sealed class CommerceOrderEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string BuyerUserId { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<CommerceOrderLineEntity> Lines { get; set; } = [];
}

public sealed class CommerceProductEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public decimal WholesaleUnitPrice { get; set; }
    public int MinOrderQty { get; set; }
    public bool SupportsPrivateLabel { get; set; }
    public bool SupportsExport { get; set; }
}

public sealed class CommerceOrderLineEntity
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class CommerceCartEntity
{
    public string TenantKey { get; set; } = "";
    public string UserId { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public List<CommerceCartLineEntity> Lines { get; set; } = [];
}

public sealed class CommerceCartLineEntity
{
    public string TenantKey { get; set; } = "";
    public string UserId { get; set; } = "";
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}

public sealed class CommerceProfileEntity
{
    public string TenantKey { get; set; } = "";
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string PriceTier { get; set; } = "standard";
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class CommerceNotificationEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsRead { get; set; }
}

public sealed class CommerceRfqEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string BuyerUserId { get; set; } = "";
    public string Status { get; set; } = "submitted";
    public string Message { get; set; } = "";
    public decimal? QuotedTotal { get; set; }
    public string? OperatorNotes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
    public List<CommerceRfqLineEntity> Lines { get; set; } = [];
}

public sealed class CommerceRfqLineEntity
{
    public Guid Id { get; set; }
    public Guid RfqId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}

public sealed class CommerceOutboxMessageEntity
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? ProcessedOn { get; set; }
    public string Status { get; set; } = "Pending";
    public int RetryCount { get; set; }
    public string? Error { get; set; }
}

public sealed class PostgresCommerceOrderPersistence(IDbContextFactory<CommerceDbContext> dbFactory)
    : ICommerceOrderPersistence
{
    public async Task SaveOrderAndOutboxAsync(
        CommerceOrderSnapshot order,
        CommerceOrderPlacedV1 @event,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (!await db.Orders.AnyAsync(x => x.Id == order.Id, cancellationToken))
        {
            db.Orders.Add(new CommerceOrderEntity
            {
                Id = order.Id,
                TenantKey = order.TenantKey,
                BuyerUserId = order.BuyerUserId,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                CreatedAt = order.CreatedAt,
                Lines = order.Lines.Select(line => new CommerceOrderLineEntity
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ProductId = line.ProductId,
                    Sku = line.Sku,
                    Name = line.Name,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                }).ToList(),
            });
        }

        if (!await db.OutboxMessages.AnyAsync(x => x.Id == @event.EventId, cancellationToken))
        {
            db.OutboxMessages.Add(new CommerceOutboxMessageEntity
            {
                Id = @event.EventId,
                Type = "Commerce.OrderPlaced.v1",
                Content = JsonSerializer.Serialize(@event),
                OccurredAt = @event.OccurredAt,
                Status = "Pending",
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommerceOrderView>> GetOrdersAsync(
        string tenantKey,
        string? buyerUserId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Orders.AsNoTracking()
            .Include(order => order.Lines)
            .Where(order => order.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(buyerUserId))
            query = query.Where(order => order.BuyerUserId == buyerUserId);

        var orders = await query
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);
        return orders.Select(ToView).ToArray();
    }

    public async Task<CommerceOrderView?> GetOrderAsync(
        Guid orderId,
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var order = await db.Orders.AsNoTracking()
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == orderId && item.TenantKey == tenantKey, cancellationToken);
        return order is null ? null : ToView(order);
    }

    public async Task<CommerceOrderView?> UpdateOrderStatusAsync(
        Guid orderId,
        string tenantKey,
        string status,
        CancellationToken cancellationToken = default)
    {
        var normalized = status.Trim().ToLowerInvariant();
        if (normalized is not ("pending" or "confirmed" or "shipped" or "cancelled"))
            return null;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var order = await db.Orders
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == orderId && item.TenantKey == tenantKey, cancellationToken);
        if (order is null)
            return null;

        order.Status = normalized;
        await db.SaveChangesAsync(cancellationToken);
        return ToView(order);
    }

    private static CommerceOrderView ToView(CommerceOrderEntity order) =>
        new(
            order.Id,
            order.TenantKey,
            order.BuyerUserId,
            order.Status,
            order.TotalAmount,
            order.CreatedAt,
            order.Lines
                .OrderBy(line => line.Id)
                .Select(line => new CommerceOrderLineSnapshot(
                    line.ProductId,
                    line.Sku,
                    line.Name,
                    line.Quantity,
                    line.UnitPrice))
                .ToArray());
}

public sealed class PostgresCommerceCatalogPersistence(IDbContextFactory<CommerceDbContext> dbFactory) : ICommerceCatalogPersistence
{
    public async Task<IReadOnlyList<CommerceProductSnapshot>> GetProductsAsync(string tenantKey, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var products = await db.Products.AsNoTracking().Where(x => x.TenantKey == tenantKey).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return products.Select(ToSnapshot).ToArray();
    }

    public async Task SaveProductsAsync(IReadOnlyList<CommerceProductSnapshot> products, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        foreach (var product in products)
        {
            var entity = await db.Products.SingleOrDefaultAsync(x => x.Id == product.Id, cancellationToken);
            if (entity is null)
            {
                entity = new CommerceProductEntity { Id = product.Id };
                db.Products.Add(entity);
            }
            entity.TenantKey = product.TenantKey.Trim();
            entity.Sku = product.Sku.Trim();
            entity.Name = product.Name.Trim();
            entity.Description = product.Description.Trim();
            entity.UnitPrice = product.UnitPrice;
            entity.WholesaleUnitPrice = product.WholesaleUnitPrice;
            entity.MinOrderQty = Math.Max(1, product.MinOrderQty);
            entity.SupportsPrivateLabel = product.SupportsPrivateLabel;
            entity.SupportsExport = product.SupportsExport;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static CommerceProductSnapshot ToSnapshot(CommerceProductEntity x) => new(x.Id, x.TenantKey, x.Sku, x.Name, x.Description, x.UnitPrice, x.WholesaleUnitPrice, x.MinOrderQty, x.SupportsPrivateLabel, x.SupportsExport);
}

public sealed class PostgresCommerceCartPersistence(IDbContextFactory<CommerceDbContext> dbFactory)
    : ICommerceCartPersistence
{
    public async Task<CommerceCartSnapshot> GetCartAsync(
        string tenantKey,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var cart = await db.Carts.AsNoTracking()
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.TenantKey == tenantKey && item.UserId == userId, cancellationToken);
        return cart is null
            ? new CommerceCartSnapshot(tenantKey, userId, [])
            : new CommerceCartSnapshot(
                cart.TenantKey,
                cart.UserId,
                cart.Lines.Select(line => new CommerceCartLineSnapshot(line.ProductId, line.Quantity)).ToArray());
    }

    public async Task SaveCartAsync(
        CommerceCartSnapshot cart,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Carts
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.TenantKey == cart.TenantKey && item.UserId == cart.UserId, cancellationToken);
        if (existing is null)
        {
            existing = new CommerceCartEntity
            {
                TenantKey = cart.TenantKey,
                UserId = cart.UserId,
            };
            db.Carts.Add(existing);
        }
        else
        {
            db.CartLines.RemoveRange(existing.Lines);
        }

        existing.UpdatedAt = DateTimeOffset.UtcNow;
        existing.Lines = cart.Lines
            .Where(line => line.Quantity > 0)
            .Select(line => new CommerceCartLineEntity
            {
                TenantKey = cart.TenantKey,
                UserId = cart.UserId,
                ProductId = line.ProductId,
                Quantity = Math.Min(line.Quantity, 999),
            })
            .ToList();
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class PostgresCommerceProfilePersistence(IDbContextFactory<CommerceDbContext> dbFactory)
    : ICommerceProfilePersistence
{
    public async Task<CommerceProfileSnapshot> GetProfileAsync(
        string tenantKey,
        string userId,
        string fallbackEmail,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.Profiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantKey == tenantKey && item.UserId == userId, cancellationToken);
        return profile is null
            ? new CommerceProfileSnapshot(tenantKey, userId, fallbackEmail.Split('@')[0], fallbackEmail, "", "", "standard")
            : ToSnapshot(profile);
    }

    public async Task SaveProfileAsync(
        CommerceProfileSnapshot profile,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Profiles
            .SingleOrDefaultAsync(item => item.TenantKey == profile.TenantKey && item.UserId == profile.UserId, cancellationToken);
        if (entity is null)
        {
            entity = new CommerceProfileEntity
            {
                TenantKey = profile.TenantKey,
                UserId = profile.UserId,
            };
            db.Profiles.Add(entity);
        }

        entity.DisplayName = profile.DisplayName.Trim();
        entity.Email = profile.Email.Trim();
        entity.Phone = profile.Phone.Trim();
        entity.CompanyName = profile.CompanyName.Trim();
        entity.PriceTier = NormalizePriceTier(profile.PriceTier);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static CommerceProfileSnapshot ToSnapshot(CommerceProfileEntity profile) =>
        new(profile.TenantKey, profile.UserId, profile.DisplayName, profile.Email, profile.Phone, profile.CompanyName, profile.PriceTier);

    private static string NormalizePriceTier(string? tier) =>
        tier?.Trim().ToLowerInvariant() switch
        {
            "wholesale" => "wholesale",
            "distributor" => "distributor",
            _ => "standard",
        };
}

public sealed class PostgresCommerceNotificationPersistence(IDbContextFactory<CommerceDbContext> dbFactory)
    : ICommerceNotificationPersistence
{
    public async Task<IReadOnlyList<CommerceNotificationSnapshot>> GetNotificationsAsync(string tenantKey, string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.Notifications.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return items.Select(ToSnapshot).ToArray();
    }

    public async Task SaveNotificationAsync(CommerceNotificationSnapshot notification, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Notifications.SingleOrDefaultAsync(x => x.Id == notification.Id, cancellationToken);
        if (entity is null)
        {
            entity = new CommerceNotificationEntity { Id = notification.Id };
            db.Notifications.Add(entity);
        }
        entity.TenantKey = notification.TenantKey;
        entity.UserId = notification.UserId;
        entity.Title = notification.Title.Trim();
        entity.Message = notification.Message.Trim();
        entity.CreatedAt = notification.CreatedAt;
        entity.IsRead = notification.IsRead;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAsReadAsync(Guid id, string tenantKey, string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.TenantKey == tenantKey && x.UserId == userId, cancellationToken);
        if (entity is null) return;
        entity.IsRead = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static CommerceNotificationSnapshot ToSnapshot(CommerceNotificationEntity x) => new(x.Id, x.TenantKey, x.UserId, x.Title, x.Message, x.CreatedAt, x.IsRead);
}

public sealed class PostgresCommerceRfqPersistence(IDbContextFactory<CommerceDbContext> dbFactory) : ICommerceRfqPersistence
{
    public async Task SaveRfqAsync(CommerceRfqSnapshot rfq, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Rfqs.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == rfq.Id, cancellationToken);
        if (existing is not null) return;
        db.Rfqs.Add(new CommerceRfqEntity
        {
            Id = rfq.Id, TenantKey = rfq.TenantKey, BuyerUserId = rfq.BuyerUserId, Status = NormalizeStatus(rfq.Status),
            Message = rfq.Message.Trim(), QuotedTotal = rfq.QuotedTotal, OperatorNotes = rfq.OperatorNotes?.Trim(),
            CreatedAt = rfq.CreatedAt, RespondedAt = rfq.RespondedAt,
            Lines = rfq.Lines.Where(x => x.Quantity > 0).Select(x => new CommerceRfqLineEntity { Id = Guid.NewGuid(), RfqId = rfq.Id, ProductId = x.ProductId, Quantity = Math.Min(x.Quantity, 999999), Notes = x.Notes?.Trim() }).ToList()
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommerceRfqSnapshot>> GetRfqsAsync(string tenantKey, string? buyerUserId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Rfqs.AsNoTracking().Include(x => x.Lines).Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(buyerUserId)) query = query.Where(x => x.BuyerUserId == buyerUserId);
        var items = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return items.Select(ToSnapshot).ToArray();
    }

    public async Task<CommerceRfqSnapshot?> GetRfqAsync(Guid id, string tenantKey, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Rfqs.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id && x.TenantKey == tenantKey, cancellationToken);
        return item is null ? null : ToSnapshot(item);
    }

    public async Task<CommerceRfqSnapshot?> UpdateRfqAsync(Guid id, string tenantKey, string status, decimal quotedTotal, string operatorNotes, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeStatus(status);
        if (normalized is not ("quoted" or "declined" or "closed")) return null;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Rfqs.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id && x.TenantKey == tenantKey, cancellationToken);
        if (item is null) return null;
        item.Status = normalized; item.QuotedTotal = quotedTotal; item.OperatorNotes = operatorNotes.Trim(); item.RespondedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToSnapshot(item);
    }

    private static string NormalizeStatus(string status) => status.Trim().ToLowerInvariant();
    private static CommerceRfqSnapshot ToSnapshot(CommerceRfqEntity x) => new(x.Id, x.TenantKey, x.BuyerUserId, x.Status, x.Message, x.QuotedTotal, x.OperatorNotes, x.CreatedAt, x.RespondedAt, x.Lines.Select(l => new CommerceRfqLineSnapshot(l.ProductId, l.Quantity, l.Notes)).ToArray());
}

public static class CommerceInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddCommerceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("CommerceDb");
        if (string.IsNullOrWhiteSpace(connection))
            return services;

        services.AddHttpContextAccessor();
        services.AddHisHopeTenantAwareDbContextFactory<CommerceDbContext>(
            "commerce",
            (sp, builder, connectionString, connectionName) =>
                builder.UseHisHopeNpgsql(
                    sp,
                    sp.GetRequiredService<IConfiguration>(),
                    connectionString,
                    connectionName,
                    b => b.MigrationsAssembly(typeof(CommerceDbContext).Assembly.GetName().Name)));
        services.AddSingleton<ICommerceDbContextFactory>(sp =>
            new CommerceDbContextFactoryBridge(sp.GetRequiredService<IHisHopeDbContextFactory<CommerceDbContext>>()));
        services.AddSingleton<ICommerceOrderPersistence, PostgresCommerceOrderPersistence>();
        services.AddSingleton<ICommerceCatalogPersistence, PostgresCommerceCatalogPersistence>();
        services.AddSingleton<ICommerceCartPersistence, PostgresCommerceCartPersistence>();
        services.AddSingleton<ICommerceProfilePersistence, PostgresCommerceProfilePersistence>();
        services.AddSingleton<ICommerceNotificationPersistence, PostgresCommerceNotificationPersistence>();
        services.AddSingleton<ICommerceRfqPersistence, PostgresCommerceRfqPersistence>();
        services.AddHostedService<Messaging.CommerceOutboxDispatcher>();
        return services;
    }

    public static async Task MigrateCommerceDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetService<ICommerceDbContextFactory>();
        if (factory is null)
            return;

        foreach (var connectionName in factory.GetRegisteredConnectionNames())
        {
            await using var db = await factory.CreateDbContextForConnectionAsync(connectionName);
            await db.Database.MigrateAsync();
        }
    }
}
