using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingTraceabilityStore
{
    GenealogyDto GetGenealogy(Guid lotId, bool upstream, string tenantKey);
    RecallImpactDto GetRecallImpact(Guid lotId, string tenantKey, int maxLots = 500);
    EpcisDocumentDto GetEpcisEvents(string tenantKey, DateTimeOffset? from, DateTimeOffset? to, int limit = 500);
    bool LotBelongsToTenant(Guid lotId, string tenantKey);
    IReadOnlyList<InventoryTransactionDto> GetInventoryTransactions(Guid lotId, string tenantKey, int limit);
}
