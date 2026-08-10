namespace His.Hope.IdentityService.Application.DTOs;

public record BulkImportRequest(
    List<BulkUserRecord> Users,
    bool SendWelcomeEmail = false,
    bool SkipExisting = true);

public record BulkUserRecord(
    string UserName,
    string Email,
    string FirstName,
    string LastName,
    string? MiddleName = null,
    string? LicenseNumber = null,
    string? Specialty = null,
    string? Role = null,
    string? FacilityId = null,
    bool IsActive = true);

public record BulkImportResult(
    int TotalSubmitted,
    int Succeeded,
    int Skipped,
    int Failed,
    List<BulkImportError> Errors);

public record BulkImportError(
    string UserName,
    string Error);
