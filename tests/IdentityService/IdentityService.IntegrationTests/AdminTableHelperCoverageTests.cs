using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using His.Hope.IdentityService.Api.Endpoints;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class AdminTableHelperCoverageTests
{
    private static object? Invoke(string name, params object?[] args) =>
        typeof(AdminTableEndpoints)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, args);

    [Fact]
    public void ParseIds_discards_invalid_values_and_deduplicates()
    {
        var id = Guid.NewGuid();

        var result = (Guid[])Invoke("ParseIds", (object)new[] { "bad", id.ToString(), id.ToString(), " " })!;

        Assert.Single(result);
        Assert.Equal(id, result[0]);
    }

    [Fact]
    public void Csv_and_spreadsheet_escaping_protect_formula_values_and_quotes()
    {
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["name"] = "O\"Brien", ["formula"] = "=SUM(A1)", ["when"] = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc) }
        };

        var csv = (string)Invoke("ToCsv", rows)!;
        var safe = (string)Invoke("SpreadsheetSafe", "@import")!;

        Assert.Contains("\"O\"\"Brien\"", csv);
        Assert.Contains("\"'=SUM(A1)\"", csv);
        Assert.Equal("'@import", safe);
    }

    [Fact]
    public void Export_policy_filters_columns_and_masks_sensitive_fields()
    {
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["id"] = "1", ["email"] = "person@example.test", ["secret"] = "value" }
        };
        var requestType = typeof(AdminTableEndpoints).GetNestedType("AdminExportRequest", BindingFlags.NonPublic)!;
        var request = requestType.GetConstructor([
                typeof(string), typeof(string[]), typeof(string[]), typeof(JsonElement), typeof(bool), typeof(bool)])!
            .Invoke(["csv", new[] { "id", "email" }, Array.Empty<string>(), default(JsonElement), false, true]);

        Invoke("ApplyExportPolicy", rows, request);

        Assert.Equal(["id", "email"], rows[0].Keys);
        Assert.Equal("[REDACTED]", rows[0]["email"]);
        Assert.DoesNotContain("secret", rows[0].Keys);
    }

    [Fact]
    public void ToXlsx_produces_a_readable_zip_package()
    {
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["id"] = "1", ["name"] = "Patient" }
        };

        var bytes = (byte[])Invoke("ToXlsx", rows)!;

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.Contains(archive.Entries, entry => entry.FullName == "xl/worksheets/sheet1.xml");
    }
}
