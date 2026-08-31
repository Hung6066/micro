using Microsoft.EntityFrameworkCore;
using His.Hope.ManufacturingService.Application.Ports;
using System.Text.Json;

public sealed partial class PostgresManufacturingStore : IManufacturingQualityWorkflowStore
{
    public (ProductSpecificationDto? Specification, string? Error) CreateProductSpecification(CreateProductSpecificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.ProductSku) ||
            string.IsNullOrWhiteSpace(request.Packaging) || string.IsNullOrWhiteSpace(request.QcSpec) ||
            request.TargetMoisturePercent is < 0 or > 100 || request.ShelfLifeDays <= 0 || request.Status != ManufacturingStatusCodes.Draft)
            return (null, ManufacturingErrorCodes.InvalidProductSpecification);

        using var db = dbFactory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var entity = new ManufacturingProductSpecificationEntity
        {
            Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), ProductSku = request.ProductSku.Trim(),
            TargetMoisturePercent = request.TargetMoisturePercent, Packaging = request.Packaging.Trim(),
            ShelfLifeDays = request.ShelfLifeDays, QcSpec = request.QcSpec.Trim(), Status = ManufacturingStatusCodes.Draft, CreatedAt = now
        };
        db.ProductSpecifications.Add(entity);
        AddProductSpecificationEvent(db, entity, ManufacturingStatusCodes.Created, "system", now);
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public IReadOnlyList<ProductSpecificationDto> GetProductSpecifications(string tenantKey, string? productSku, string? status, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.ProductSpecifications.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(productSku)) query = query.Where(x => x.ProductSku == productSku);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return query.OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(limit, 1, 200)).AsEnumerable().Select(ToDto).ToList();
    }

    public (ProductSpecificationDto? Specification, string? Error) ChangeProductSpecificationLifecycle(
        Guid specificationId, string tenantKey, string targetStatus, ProductSpecificationLifecycleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Actor)) return (null, "invalid_product_specification_actor");
        using var db = dbFactory.CreateDbContext();
        var entity = db.ProductSpecifications.SingleOrDefault(x => x.Id == specificationId);
        if (entity is null) return (null, ManufacturingErrorCodes.ProductSpecificationNotFound);
        if (!entity.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantScopeDenied);
        var valid = (entity.Status, targetStatus) switch
        {
            (ManufacturingStatusCodes.Draft, ManufacturingStatusCodes.Approved) => true,
            (ManufacturingStatusCodes.Approved, "Retired") => true,
            _ => false
        };
        if (!valid) return (null, "invalid_product_specification_transition");
        if (targetStatus == ManufacturingStatusCodes.Approved && db.ProductSpecifications.Any(x => x.TenantKey == tenantKey && x.ProductSku == entity.ProductSku && x.Status == ManufacturingStatusCodes.Approved && x.Id != entity.Id))
            return (null, ManufacturingErrorCodes.ActiveProductSpecificationExists);

        var now = DateTimeOffset.UtcNow;
        entity.Status = targetStatus;
        if (targetStatus == ManufacturingStatusCodes.Approved) { entity.ApprovedBy = request.Actor.Trim(); entity.ApprovedAt = now; }
        AddProductSpecificationEvent(db, entity, targetStatus, request.Actor.Trim(), now);
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    private static void AddProductSpecificationEvent(ManufacturingDbContext db, ManufacturingProductSpecificationEntity entity, string action, string actor, DateTimeOffset occurredAt)
    {
        var eventId = Guid.NewGuid();
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = eventId, Type = $"Manufacturing.ProductSpecification{action}.v1",
            Content = JsonSerializer.Serialize(new { eventId, schemaVersion = 1, occurredAt, correlationId = entity.Id, specificationId = entity.Id, tenantKey = entity.TenantKey, productSku = entity.ProductSku, status = entity.Status, actor }),
            OccurredOn = occurredAt.UtcDateTime, Status = ManufacturingStatusCodes.Pending
        });
    }

    private static ProductSpecificationDto ToDto(ManufacturingProductSpecificationEntity x) => new(
        x.Id, x.TenantKey, x.ProductSku, x.TargetMoisturePercent, x.Packaging, x.ShelfLifeDays, x.QcSpec,
        x.Status, x.ApprovedBy, x.ApprovedAt, x.CreatedAt);
}
