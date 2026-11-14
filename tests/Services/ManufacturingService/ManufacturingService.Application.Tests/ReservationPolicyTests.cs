using His.Hope.ManufacturingService.Application;

namespace ManufacturingService.Application.Tests;

public sealed class ReservationPolicyTests
{
    private static ReservationValidationInput ValidInput() => new(
        "nacoms-vn", "nacoms-vn", "Released", null, new DateOnly(2026, 8, 25),
        Guid.NewGuid(), "SalesOrder", 10m, 5m, 20m);

    [Fact]
    public void AcceptsReleasedLotWithinAvailableQuantity()
    {
        Assert.Null(ReservationPolicy.Validate(ValidInput()));
    }

    [Theory]
    [InlineData("tenant_mismatch")]
    [InlineData("lot_not_released")]
    [InlineData("lot_expired")]
    [InlineData("reservation_exceeds_available")]
    public void RejectsUnsafeReservationStates(string expected)
    {
        var input = ValidInput() with
        {
            LotTenantKey = expected == "tenant_mismatch" ? "other-tenant" : "nacoms-vn",
            Disposition = expected == "lot_not_released" ? "Quarantined" : "Released",
            BestBefore = expected == "lot_expired" ? new DateOnly(2026, 8, 24) : null,
            ReservedQuantity = expected == "reservation_exceeds_available" ? 15m : 5m
        };

        Assert.Equal(expected, ReservationPolicy.Validate(input));
    }

    [Fact]
    public void RejectsMissingReferenceAndNonPositiveQuantity()
    {
        var input = ValidInput() with { ReferenceType = " ", ReferenceId = Guid.Empty, RequestedQuantity = 0m };

        Assert.Equal("invalid_reservation", ReservationPolicy.Validate(input));
    }
}
