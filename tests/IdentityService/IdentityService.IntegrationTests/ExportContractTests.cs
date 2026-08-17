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
}
