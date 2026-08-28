using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingReservationStore
{
    (LotReservationDto? Reservation, string? Error) Reserve(string tenantKey, Guid lotId, CreateLotReservationRequest request);
    (LotReservationDto? Reservation, string? Error) Release(string tenantKey, Guid reservationId);
    IReadOnlyList<LotReservationDto> GetReservations(string tenantKey, Guid lotId, string? status, int limit);
    IReadOnlyList<FefoLotDto> GetFefo(string tenantKey, string sku, int limit);
    (SalesAllocationDto Allocation, string? Error) AllocateSales(string tenantKey, string sku, CreateSalesAllocationRequest request);
    IReadOnlyList<SalesAllocationDto> GetSalesAllocations(string tenantKey, string? sku, Guid? salesOrderId, int limit);
}
