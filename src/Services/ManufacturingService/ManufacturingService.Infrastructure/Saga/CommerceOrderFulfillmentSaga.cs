using His.Hope.Contracts.Commerce;
using His.Hope.Infrastructure.Saga;

namespace His.Hope.ManufacturingService.Infrastructure.Saga;

public sealed class CommerceOrderFulfillmentSagaData
{
    public CommerceOrderPlacedV1 Order { get; init; } =
        new(Guid.Empty, 1, DateTimeOffset.MinValue, Guid.Empty, string.Empty, string.Empty, 0, []);

    public List<Guid> ReservationIds { get; set; } = [];

    public CommerceOrderFulfillmentSagaData(CommerceOrderPlacedV1 order) => Order = order;

    public CommerceOrderFulfillmentSagaData() { }
}

public sealed class CommerceOrderFulfillmentSagaStep(
    ManufacturingReservationStore reservationStore) : ISagaStep<CommerceOrderFulfillmentSagaData>
{
    public Task ExecuteAsync(CommerceOrderFulfillmentSagaData data, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var result = reservationStore.AllocateCommerceOrder(data.Order);
        if (result.Error is not null)
            throw new InvalidOperationException(result.Error);

        // On redelivery, the transactional event receipt makes allocation a
        // no-op. Reload the existing allocation so compensation remains safe.
        data.ReservationIds = result.Allocations
            .SelectMany(allocation => allocation.Reservations)
            .Select(reservation => reservation.Id)
            .Distinct()
            .ToList();
        if (data.ReservationIds.Count == 0)
        {
            data.ReservationIds = reservationStore
                .GetSalesAllocations(data.Order.TenantKey, null, data.Order.OrderId, 200)
                .SelectMany(allocation => allocation.Reservations)
                .Select(reservation => reservation.Id)
                .Distinct()
                .ToList();
        }

        return Task.CompletedTask;
    }

    public Task CompensateAsync(CommerceOrderFulfillmentSagaData data, CancellationToken ct = default)
    {
        foreach (var reservationId in data.ReservationIds)
        {
            ct.ThrowIfCancellationRequested();
            var result = reservationStore.Release(data.Order.TenantKey, reservationId);
            if (result.Error is not null && result.Error != ManufacturingErrorCodes.ReservationNotFound)
                throw new InvalidOperationException(result.Error);
        }

        return Task.CompletedTask;
    }
}
