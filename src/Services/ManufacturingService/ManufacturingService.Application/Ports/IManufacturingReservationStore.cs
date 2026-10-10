using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingReservationStore
{
    Task<(LotReservationDto? Reservation, string? Error)> ReserveAsync(string tenantKey, Guid lotId, CreateLotReservationRequest request, CancellationToken cancellationToken = default);
    Task<(LotReservationDto? Reservation, string? Error)> ReleaseAsync(string tenantKey, Guid reservationId, CancellationToken cancellationToken = default);
    IReadOnlyList<LotReservationDto> GetReservations(string tenantKey, Guid lotId, string? status, int limit);
    IReadOnlyList<FefoLotDto> GetFefo(string tenantKey, string sku, int limit);
    Task<(SalesAllocationDto Allocation, string? Error)> AllocateSalesAsync(string tenantKey, string sku, CreateSalesAllocationRequest request, CancellationToken cancellationToken = default);
    IReadOnlyList<SalesAllocationDto> GetSalesAllocations(string tenantKey, string? sku, Guid? salesOrderId, int limit);
}
