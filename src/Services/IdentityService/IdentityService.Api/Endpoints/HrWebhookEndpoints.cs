using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class HrWebhookEndpoints
{
    public static RouteGroupBuilder MapHrWebhookEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/webhook/hr", ProcessHrWebhook);
        return group;
    }

    private static async Task<Results<Ok<HrWebhookResponse>, ProblemHttpResult>> ProcessHrWebhook(
        HrWebhookEvent hrEvent, BulkUserImportService importService, CancellationToken ct)
    {
        switch (hrEvent.EventType.ToLowerInvariant())
        {
            case "employee.hired":
            case "employee.updated":
                var record = new BulkUserRecord(
                    UserName: hrEvent.Employee.Email ?? hrEvent.Employee.EmployeeId ?? "",
                    Email: hrEvent.Employee.Email ?? "",
                    FirstName: hrEvent.Employee.FirstName ?? "",
                    LastName: hrEvent.Employee.LastName ?? "",
                    MiddleName: null,
                    LicenseNumber: hrEvent.Employee.LicenseNumber,
                    Specialty: hrEvent.Employee.Department,
                    Role: MapDepartmentToRole(hrEvent.Employee.Department),
                    FacilityId: hrEvent.Employee.FacilityId,
                    IsActive: hrEvent.EventType == "employee.hired"
                );

                var importRequest = new BulkImportRequest(
                    new List<BulkUserRecord> { record },
                    SkipExisting: false
                );

                var result = await importService.ImportAsync(importRequest, ct);
                return TypedResults.Ok(new HrWebhookResponse(
                    result.Succeeded > 0 ? "provisioned" : "error",
                    hrEvent.Employee.EmployeeId ?? "",
                    result.Errors.FirstOrDefault()?.Error));

            case "employee.terminated":
                return TypedResults.Ok(new HrWebhookResponse("acknowledged",
                    hrEvent.Employee.EmployeeId ?? "", "User deactivation handled via SCIM PATCH"));

            default:
                return TypedResults.Problem($"Unsupported event type: {hrEvent.EventType}", statusCode: 400);
        }
    }

    private static string? MapDepartmentToRole(string? department)
    {
        return department?.ToLowerInvariant() switch
        {
            "nursing" => "Nurse",
            "laboratory" => "LabTechnician",
            "pharmacy" => "Pharmacist",
            "billing" => "BillingClerk",
            "reception" => "Receptionist",
            "medical" => "Provider",
            _ => "Provider"
        };
    }
}

public record HrWebhookEvent(
    string EventType,
    string EventId,
    DateTime Timestamp,
    HrEmployee Employee);

public record HrEmployee(
    string? EmployeeId,
    string? Email,
    string? FirstName,
    string? LastName,
    string? Department,
    string? LicenseNumber,
    string? FacilityId);

public record HrWebhookResponse(string Status, string EmployeeId, string? Error);
