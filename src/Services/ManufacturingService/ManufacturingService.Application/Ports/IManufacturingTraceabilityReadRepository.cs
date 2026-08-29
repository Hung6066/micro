using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingTraceabilityReadRepository
{
    GenealogyDto GetGenealogy(Guid lotId, bool upstream, string tenantKey);
    RecallImpactDto GetRecallImpact(Guid lotId, string tenantKey, int maxLots);
    Task<EpcisDocumentDto> GetEpcisEventsAsync(string tenantKey, DateTimeOffset? from, DateTimeOffset? to, int limit, int page, CancellationToken cancellationToken = default);
    bool LotBelongsToTenant(Guid lotId, string tenantKey);
    Task<IReadOnlyList<InventoryTransactionDto>> GetInventoryTransactionsAsync(Guid lotId, string tenantKey, int limit, int page, CancellationToken cancellationToken = default);
}
