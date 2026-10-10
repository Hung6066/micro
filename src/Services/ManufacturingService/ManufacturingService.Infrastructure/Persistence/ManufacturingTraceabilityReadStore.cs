using System.Text.Json;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.Contracts.Manufacturing;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.Persistence.Querying;
using Microsoft.EntityFrameworkCore;

public sealed class ManufacturingTraceabilityReadStore(IDbContextFactory<ManufacturingDbContext> dbFactory) : IManufacturingTraceabilityReadRepository
{
    public bool LotBelongsToTenant(Guid lotId, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        return db.Lots.Any(x => x.Id == lotId && x.TenantKey == tenantKey);
    }

    public GenealogyDto GetGenealogy(Guid lotId, bool upstream, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var lot = db.Lots.TagUseCase("Manufacturing.Traceability.GetLot")
            .Single(x => x.Id == lotId && x.TenantKey == tenantKey);
        var allTransformations = db.Transformations.AsNoTracking().Include(x => x.Inputs).Where(x => x.TenantKey == tenantKey).ToList();
        var linkedLotIds = new HashSet<Guid> { lotId };
        var frontier = new HashSet<Guid> { lotId };
        var visitedTransformations = new HashSet<Guid>();
        var relations = new List<LotRelationDto>();
        for (var depth = 0; depth < 32 && frontier.Count > 0 && linkedLotIds.Count < 2000; depth++)
        {
            var next = new HashSet<Guid>();
            foreach (var transformation in allTransformations)
            {
                if (visitedTransformations.Contains(transformation.Id)) continue;
                var touchesFrontier = upstream
                    ? frontier.Contains(transformation.OutputLotId)
                    : transformation.Inputs.Any(input => frontier.Contains(input.LotId));
                if (!touchesFrontier) continue;
                visitedTransformations.Add(transformation.Id);
                foreach (var input in transformation.Inputs)
                {
                    relations.Add(new LotRelationDto(transformation.Id, input.LotId, "", "input", input.Quantity));
                    if (upstream && linkedLotIds.Add(input.LotId)) next.Add(input.LotId);
                }
                relations.Add(new LotRelationDto(transformation.Id, transformation.OutputLotId, "", "output", transformation.OutputQuantity));
                if (!upstream && linkedLotIds.Add(transformation.OutputLotId)) next.Add(transformation.OutputLotId);
            }
            frontier = next;
        }
        var linkedLots = db.Lots.AsNoTracking().Where(x => linkedLotIds.Contains(x.Id) && x.TenantKey == tenantKey).ToDictionary(x => x.Id);
        relations = relations.Where(x => linkedLots.ContainsKey(x.LotId)).Select(x => x with { Sku = linkedLots[x.LotId].Sku }).ToList();
        return new GenealogyDto(ToLotDto(lot), relations);
    }

    public RecallImpactDto GetRecallImpact(Guid lotId, string tenantKey, int maxLots = 500)
    {
        using var db = dbFactory.CreateDbContext();
        var root = db.Lots.AsNoTracking().Single(x => x.Id == lotId && x.TenantKey == tenantKey);
        var transformations = db.Transformations.AsNoTracking().Include(x => x.Inputs).Where(x => x.TenantKey == tenantKey).ToList();
        var impacted = new HashSet<Guid> { root.Id };
        var frontier = new HashSet<Guid> { root.Id };
        for (var depth = 0; depth < 20 && frontier.Count > 0 && impacted.Count < maxLots; depth++)
        {
            var next = transformations.Where(t => t.Inputs.Any(i => frontier.Contains(i.LotId)))
                .Select(t => t.OutputLotId).Where(id => !impacted.Contains(id)).Take(maxLots - impacted.Count).ToHashSet();
            if (next.Count == 0) break;
            foreach (var id in next) impacted.Add(id);
            frontier = next;
        }
        var lots = db.Lots.AsNoTracking().Where(x => impacted.Contains(x.Id) && x.TenantKey == tenantKey).ToList();
        var batches = db.ProductionBatches.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.OutputLotId.HasValue && impacted.Contains(x.OutputLotId.Value))
            .ToDictionary(x => x.OutputLotId!.Value, x => x.BatchNumber);
        var result = lots.OrderBy(x => x.Id == root.Id ? 0 : 1).ThenBy(x => x.LotCode)
            .Select(x => new RecallImpactLotDto(x.Id, x.Sku, x.LotCode, x.Disposition, x.Quantity, x.Uom,
                x.Id == root.Id ? "root" : "downstream", batches.GetValueOrDefault(x.Id))).ToList();
        return new RecallImpactDto(root.Id, tenantKey, result.Count, batches.Keys.Count(id => impacted.Contains(id)), result, DateTimeOffset.UtcNow);
    }

    public Task<EpcisDocumentDto> GetEpcisEventsAsync(DateTimeOffset? from, DateTimeOffset? to, int limit, int page, CancellationToken cancellationToken = default) =>
        GetEpcisEventsAsync(HisHopeTenantScope.Current ?? throw new InvalidOperationException("Tenant context is required."), from, to, limit, page, cancellationToken);

    public async Task<EpcisDocumentDto> GetEpcisEventsAsync(string tenantKey, DateTimeOffset? from, DateTimeOffset? to, int limit = HisHopePaginationDefaults.ExportDefaultPageSize, int page = HisHopePaginationDefaults.FirstPage, CancellationToken cancellationToken = default)
    {
        using var db = dbFactory.CreateDbContext();
        var start = from?.UtcDateTime ?? DateTime.UtcNow.AddDays(-30);
        var end = to?.UtcDateTime ?? DateTime.UtcNow;
        var outboxMessages = await db.OutboxMessages.AsNoTracking().Where(x => x.OccurredOn >= start && x.OccurredOn <= end)
            .TagUseCase("Manufacturing.Traceability.GetEpcisEvents")
            .OrderBy(x => x.OccurredOn).ApplyPage(page, limit, 5000).ToListAsync(cancellationToken);
        var events = outboxMessages.Select(x =>
        {
            try
            {
                using var json = JsonDocument.Parse(x.Content);
                if (!json.RootElement.TryGetProperty("tenantKey", out var tenant) || !string.Equals(tenant.GetString(), tenantKey, StringComparison.OrdinalIgnoreCase)) return null;
                var eventId = json.RootElement.TryGetProperty("eventId", out var id) && Guid.TryParse(id.GetString(), out var parsed) ? parsed : x.Id;
                var occurred = json.RootElement.TryGetProperty("occurredAt", out var at) && at.TryGetDateTimeOffset(out var parsedAt) ? parsedAt : new DateTimeOffset(x.OccurredOn, TimeSpan.Zero);
                return new EpcisEventDto(eventId, x.Type, occurred, x.Content);
            }
            catch (JsonException) { return null; }
        }).Where(x => x is not null).Cast<EpcisEventDto>().ToList();
        return new EpcisDocumentDto($"urn:his-hope:manufacturing:{Guid.NewGuid():N}", "2.0", "EPCISDocument", events, DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<InventoryTransactionDto>> GetInventoryTransactionsAsync(Guid lotId, string tenantKey, int limit, int page = 1, CancellationToken cancellationToken = default)
    {
        using var db = dbFactory.CreateDbContext();
        return await db.InventoryTransactions.AsNoTracking()
            .TagUseCase("Manufacturing.Traceability.GetInventoryTransactions")
            .Where(x => x.LotId == lotId && x.TenantKey == tenantKey)
            .OrderByDescending(x => x.OccurredAt).ApplyPage(page, limit)
            .Select(x => new InventoryTransactionDto(x.Id, x.TenantKey, x.LotId, x.TransactionType, x.Quantity, x.Uom, x.FacilityId, x.StockStatus, x.CorrelationId, x.OccurredAt))
            .ToListAsync(cancellationToken);
    }

    private static LotDto ToLotDto(ManufacturingLotEntity x) =>
        new(x.Id, x.TenantKey, x.Sku, x.Quantity, x.Uom, x.Disposition, x.BestBefore, x.CreatedAt,
            x.LotCode, x.LotType, x.OriginCountryCode, x.ManufacturedOn, x.ReceivedAt, x.FacilityCode,
            x.StorageLocationCode, x.CertificateOfAnalysisReference, x.SourceLotCode, x.QualityStatus, x.CreatedBy, x.UpdatedAt);
}
