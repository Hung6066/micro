using System.IO.Compression;
using System.Xml.Linq;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class BulkImportEndpoints
{
    public static RouteGroupBuilder MapBulkImportEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/users/bulk", BulkImportUsers)
            .RequireAuthorization("Permission:admin.users.write");
        group.MapPost("/users/bulk/csv", BulkImportCsv)
            .RequireAuthorization("Permission:admin.users.write");
        group.MapPost("/users/bulk/file", BulkImportFile)
            .RequireAuthorization("Permission:admin.users.write");
        group.MapPost("/users/bulk/preview", PreviewImport)
            .RequireAuthorization("Permission:admin.users.read");
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

    private static async Task<IResult> BulkImportFile(HttpRequest httpRequest, BulkUserImportService importService, CancellationToken ct)
    {
        try
        {
            var users = await ParseImportAsync(httpRequest, ct);
            if (users.Count == 0) return Results.Problem("No valid records found in the import file.", statusCode: 400);
            if (users.Count > 10000) return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Import is limited to 10000 users."] });
            return Results.Ok(await importService.ImportAsync(new BulkImportRequest(users, SendWelcomeEmail: false, SkipExisting: true), ct));
        }
        catch (InvalidDataException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [exception.Message] }); }
        catch (InvalidOperationException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [exception.Message] }); }
    }

    private static async Task<IResult> PreviewImport(HttpRequest httpRequest, CancellationToken ct)
    {
        List<BulkUserRecord> users;
        try { users = await ParseImportAsync(httpRequest, ct); }
        catch (InvalidDataException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [exception.Message] }); }
        catch (InvalidOperationException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [exception.Message] }); }
        if (users.Count > 1000) return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Preview is limited to 1000 rows."] });
        var rows = users.Select((user, index) => new
        {
            row = index + 2,
            valid = !string.IsNullOrWhiteSpace(user.UserName) && !string.IsNullOrWhiteSpace(user.Email) && user.Email.Contains('@', StringComparison.Ordinal),
            user
        }).ToArray();
        return Results.Ok(new { total = rows.Length, valid = rows.Count(row => row.valid), invalid = rows.Count(row => !row.valid), rows });
    }

    private static async Task<List<BulkUserRecord>> ParseImportAsync(HttpRequest request, CancellationToken ct)
    {
        if (request.ContentLength is > 10 * 1024 * 1024)
            throw new InvalidOperationException("Import is limited to 10 MB.");

        var isXlsx = request.ContentType?.Contains("spreadsheetml", StringComparison.OrdinalIgnoreCase) == true ||
                     request.Headers.ContentDisposition.Any(value => value.Contains(".xlsx", StringComparison.OrdinalIgnoreCase));
        if (isXlsx)
        {
            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer, ct);
            return ParseXlsx(buffer.ToArray());
        }

        using var reader = new StreamReader(request.Body);
        return ParseCsv(await reader.ReadToEndAsync(ct));
    }

    private static List<BulkUserRecord> ParseCsv(string csv)
    {
        var records = new List<BulkUserRecord>();
        var lines = csv.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return records;

        var headerFields = ParseCsvLine(lines[0]);
        var headers = headerFields.Select(h => h.Trim().ToLowerInvariant()).ToList();

        for (var i = 1; i < lines.Length; i++)
        {
            var fields = ParseCsvLine(lines[i]);
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

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var value = new System.Text.StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"') { value.Append('"'); index++; }
                else quoted = !quoted;
            }
            else if (character == ',' && !quoted) { fields.Add(value.ToString().Trim()); value.Clear(); }
            else value.Append(character);
        }
        fields.Add(value.ToString().Trim());
        return fields.ToArray();
    }

    private static List<BulkUserRecord> ParseXlsx(byte[] content)
    {
        using var input = new MemoryStream(content);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        var entry = archive.GetEntry("xl/worksheets/sheet1.xml") ?? throw new InvalidOperationException("The XLSX workbook has no first worksheet.");
        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sharedStrings = archive.GetEntry("xl/sharedStrings.xml") is { } stringsEntry
            ? ReadSharedStrings(stringsEntry, ns)
            : [];
        var rows = document.Descendants(ns + "row").Select(row => row.Elements(ns + "c").Select(cell =>
        {
            var value = cell.Element(ns + "v")?.Value ?? cell.Element(ns + "is")?.Element(ns + "t")?.Value ?? string.Empty;
            return cell.Attribute("t")?.Value == "s" && int.TryParse(value, out var index) && index >= 0 && index < sharedStrings.Count ? sharedStrings[index] : value;
        }).ToArray()).ToArray();
        if (rows.Length < 2) return [];
        var headers = rows[0].Select(value => value.Trim().ToLowerInvariant()).ToList();
        return rows.Skip(1).Select(fields => CreateRecord(fields, headers)).Where(record => record is not null).Select(record => record!).ToList();
    }

    private static List<string> ReadSharedStrings(ZipArchiveEntry entry, XNamespace ns)
    {
        using var stream = entry.Open();
        return XDocument.Load(stream).Descendants(ns + "si").Select(item => string.Concat(item.Descendants(ns + "t").Select(text => text.Value))).ToList();
    }

    private static BulkUserRecord? CreateRecord(string[] fields, List<string> headers)
    {
        var userName = GetField(fields, headers, "username") ?? GetField(fields, headers, "email");
        var email = GetField(fields, headers, "email") ?? string.Empty;
        return string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email)
            ? null
            : new BulkUserRecord(userName, email, GetField(fields, headers, "firstname") ?? string.Empty, GetField(fields, headers, "lastname") ?? string.Empty,
                GetField(fields, headers, "middlename"), GetField(fields, headers, "licensenumber"), GetField(fields, headers, "specialty"),
                GetField(fields, headers, "role"), GetField(fields, headers, "facilityid"));
    }

    private static string? GetField(string[] fields, List<string> headers, string name)
    {
        var idx = headers.IndexOf(name);
        return idx >= 0 && idx < fields.Length ? fields[idx].Trim().Trim('"') : null;
    }
}
