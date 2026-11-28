namespace His.Hope.ManufacturingService.Domain;

public sealed record LotTraceabilityProfile(
    string LotCode,
    string LotType,
    string? OriginCountryCode,
    DateOnly? ManufacturedOn,
    DateOnly? BestBefore,
    string? FacilityCode,
    string? StorageLocationCode);

public static class LotTraceabilityPolicy
{
    private static readonly string[] AllowedLotTypes = ["RawMaterial", "WorkInProgress", "FinishedGood", "Packaging", "Unspecified"];

    public static string? Validate(LotTraceabilityProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.LotCode) || profile.LotCode.Length > 100)
            return "invalid_lot_code";
        if (!AllowedLotTypes.Contains(profile.LotType, StringComparer.OrdinalIgnoreCase))
            return "invalid_lot_type";
        if (profile.OriginCountryCode is { Length: > 0 } country && (country.Length != 2 || country.Any(c => !char.IsLetter(c))))
            return "invalid_origin_country_code";
        if (profile.ManufacturedOn is { } manufacturedOn && profile.BestBefore is { } bestBefore && manufacturedOn > bestBefore)
            return "manufactured_after_best_before";
        if (profile.StorageLocationCode is { Length: > 0 } && string.IsNullOrWhiteSpace(profile.FacilityCode))
            return "storage_location_requires_facility";
        return null;
    }
}
