using His.Hope.ManufacturingService.Domain;

namespace His.Hope.ManufacturingService.Application;

public sealed record ReservationValidationInput(
    string TenantKey,
    string LotTenantKey,
    string Disposition,
    DateOnly? BestBefore,
    DateOnly Today,
    Guid ReferenceId,
    string ReferenceType,
    decimal RequestedQuantity,
    decimal ReservedQuantity,
    decimal LotQuantity);

public static class ReservationPolicy
{
    public static string? Validate(ReservationValidationInput input)
    {
        if (input.RequestedQuantity <= 0 || input.ReferenceId == Guid.Empty || string.IsNullOrWhiteSpace(input.ReferenceType))
            return "invalid_reservation";
        if (!input.TenantKey.Equals(input.LotTenantKey, StringComparison.OrdinalIgnoreCase))
        return ManufacturingErrorCodes.TenantMismatch;
        if (!input.Disposition.Equals(ManufacturingStatusCodes.Released, StringComparison.OrdinalIgnoreCase))
            return ManufacturingErrorCodes.LotNotReleased;
        if (input.BestBefore is { } expiry && expiry < input.Today)
            return ManufacturingErrorCodes.LotExpired;
        if (input.ReservedQuantity + input.RequestedQuantity > input.LotQuantity)
            return ManufacturingErrorCodes.ReservationExceedsAvailable;
        return null;
    }
}
