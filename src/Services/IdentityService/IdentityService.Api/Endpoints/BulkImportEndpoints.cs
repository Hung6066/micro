using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class BulkImportEndpoints
{
    public static RouteGroupBuilder MapBulkImportEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/users/bulk", BulkImportUsers);
        group.MapPost("/users/bulk/csv", BulkImportCsv);
        return group;
    }

    private static async Task<Results<Ok<BulkImportResult>, ProblemHttpResult>> BulkImportUsers(
        BulkImportRequest request, BulkUserImportService importService, CancellationToken ct)
    {
        if (request.Users.Count == 0)
            return TypedResults.Problem("No users provided", statusCode: 400);
        if (request.Users.Count > 10000)
            return TypedResults.Problem("Maximum 10000 users per batch", statusCode: 400);

        var result = await importService.ImportAsync(request, ct);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<BulkImportResult>, ProblemHttpResult>> BulkImportCsv(
        HttpRequest httpRequest, BulkUserImportService importService, CancellationToken ct)
    {
        using var reader = new StreamReader(httpRequest.Body);
        var csvContent = await reader.ReadToEndAsync();

        var users = ParseCsv(csvContent);
        if (users.Count == 0)
            return TypedResults.Problem("No valid records found in CSV", statusCode: 400);

        var request = new BulkImportRequest(users, SendWelcomeEmail: false, SkipExisting: true);
        var result = await importService.ImportAsync(request, ct);
        return TypedResults.Ok(result);
    }

    private static List<BulkUserRecord> ParseCsv(string csv)
    {
        var records = new List<BulkUserRecord>();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return records;

        var headers = lines[0].Split(',').Select(h => h.Trim().ToLowerInvariant()).ToList();

        for (int i = 1; i < lines.Length; i++)
        {
            var fields = lines[i].Split(',');
            if (fields.Length < 4) continue;

            var record = new BulkUserRecord(
                UserName: GetField(fields, headers, "username") ?? GetField(fields, headers, "email"),
                Email: GetField(fields, headers, "email") ?? "",
                FirstName: GetField(fields, headers, "firstname") ?? "",
                LastName: GetField(fields, headers, "lastname") ?? "",
                MiddleName: GetField(fields, headers, "middlename"),
                LicenseNumber: GetField(fields, headers, "licensenumber"),
                Specialty: GetField(fields, headers, "specialty"),
                Role: GetField(fields, headers, "role"),
                FacilityId: GetField(fields, headers, "facilityid")
            );

            if (!string.IsNullOrEmpty(record.UserName) && !string.IsNullOrEmpty(record.Email))
                records.Add(record);
        }

        return records;
    }

    private static string? GetField(string[] fields, List<string> headers, string name)
    {
        var idx = headers.IndexOf(name);
        return idx >= 0 && idx < fields.Length ? fields[idx].Trim().Trim('"') : null;
    }
}
