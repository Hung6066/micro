using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingTraceabilityStore
{
    GenealogyDto GetGenealogy(Guid lotId, bool upstream, string tenantKey);
    RecallImpactDto GetRecallImpact(Guid lotId, string tenantKey, int maxLots = 500);
    Task<EpcisDocumentDto> GetEpcisEventsAsync(string tenantKey, DateTimeOffset? from, DateTimeOffset? to, int limit = 500, int page = 1, CancellationToken cancellationToken = default);
    bool LotBelongsToTenant(Guid lotId, string tenantKey);
    Task<IReadOnlyList<InventoryTransactionDto>> GetInventoryTransactionsAsync(Guid lotId, string tenantKey, int limit, int page = 1, CancellationToken cancellationToken = default);
}
