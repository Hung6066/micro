using System.Reflection;
using His.Hope.IdentityService.Api.Endpoints;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class ExportContractTests
{
    [Fact]
    public void CsvExportNeutralizesFormulaCells()
    {
        var method = typeof(AdminTableEndpoints).GetMethod("ToCsv", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["value"] = "=HYPERLINK(\"https://evil.test\")" },
            new() { ["value"] = "@cmd" }
        };

        var csv = (string)method!.Invoke(null, [rows])!;

        Assert.Contains("'=HYPERLINK", csv, StringComparison.Ordinal);
        Assert.Contains("'@cmd", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Spreadsheet_export_neutralizes_formula_cells_and_handles_safe_values()
    {
        var method = typeof(AdminTableEndpoints).GetMethod("SpreadsheetSafe", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        Assert.Equal("'=formula", method!.Invoke(null, ["=formula"]));
        Assert.Equal("'+formula", method.Invoke(null, ["+formula"]));
        Assert.Equal("'-formula", method.Invoke(null, ["-formula"]));
        Assert.Equal("'@formula", method.Invoke(null, ["@formula"]));
        Assert.Equal("plain", method.Invoke(null, ["plain"]));
        Assert.Equal(string.Empty, method.Invoke(null, [string.Empty]));
    }

    [Fact]
    public void Csv_export_handles_empty_rows_dates_quotes_and_missing_columns()
    {
        var method = typeof(AdminTableEndpoints).GetMethod("ToCsv", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        Assert.Equal(string.Empty, method!.Invoke(null, [new List<Dictionary<string, object?>>() ]));
        var rows = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["date"] = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc),
                ["quoted"] = "a\"b"
            },
            new() { ["other"] = "value" }
        };

        var csv = (string)method.Invoke(null, [rows])!;
        Assert.Contains("2026-08-24T12:00:00.0000000Z", csv, StringComparison.Ordinal);
        Assert.Contains("a\"\"b", csv, StringComparison.Ordinal);
        Assert.Contains("\"\"", csv, StringComparison.Ordinal);
    }
}
