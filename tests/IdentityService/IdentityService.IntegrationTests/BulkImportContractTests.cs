using System.Reflection;
using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using His.Hope.IdentityService.Api.Endpoints;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class BulkImportContractTests
{
    [Fact]
    public void Csv_parser_supports_quoted_commas_and_skips_incomplete_rows()
    {
        var method = typeof(BulkImportEndpoints).GetMethod("ParseCsv", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        const string csv = "username,email,firstname,lastname\n\"alice\",alice@example.com,\"A,lice\",User\nmissing,email@example.com,Only\n";
        var records = (System.Collections.IEnumerable)method!.Invoke(null, [csv])!;
        var values = records.Cast<object>().ToArray();

        Assert.Single(values);
        Assert.Equal("alice", values[0].GetType().GetProperty("UserName")!.GetValue(values[0]));
        Assert.Equal("A,lice", values[0].GetType().GetProperty("FirstName")!.GetValue(values[0]));
    }

    [Fact]
    public void Csv_parser_uses_email_as_username_when_username_column_is_empty()
    {
        var method = typeof(BulkImportEndpoints).GetMethod("ParseCsv", BindingFlags.NonPublic | BindingFlags.Static)!;
        const string csv = "username,email,firstname,lastname\n,person@example.com,Person,User\n";

        var records = ((System.Collections.IEnumerable)method.Invoke(null, [csv])!).Cast<object>().ToArray();

        Assert.Single(records);
        Assert.Equal("person@example.com", records[0].GetType().GetProperty("UserName")!.GetValue(records[0]));
    }

    [Fact]
    public void Csv_parser_rejects_header_only_and_preserves_optional_columns()
    {
        var method = typeof(BulkImportEndpoints).GetMethod("ParseCsv", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Empty(((System.Collections.IEnumerable)method.Invoke(null, ["username,email,firstname,lastname\n"])!).Cast<object>());

        const string csv = "username,email,firstname,lastname,middlename,licensenumber,specialty,role,facilityid\nuser,user@example.com,First,Last,Middle,LIC-1,Cardiology,Provider,FAC-1\n";
        var record = ((System.Collections.IEnumerable)method.Invoke(null, [csv])!).Cast<object>().Single();
        Assert.Equal("Middle", record.GetType().GetProperty("MiddleName")!.GetValue(record));
        Assert.Equal("FAC-1", record.GetType().GetProperty("FacilityId")!.GetValue(record));
    }

    [Fact]
    public void Csv_parser_handles_escaped_quotes_and_missing_headers()
    {
        var method = typeof(BulkImportEndpoints).GetMethod("ParseCsv", BindingFlags.NonPublic | BindingFlags.Static)!;
        const string csv = "email,username,firstname,lastname\nquoted@example.com,quoted,\"A \"\"quoted\"\" name\",User\n";

        var record = ((System.Collections.IEnumerable)method.Invoke(null, [csv])!).Cast<object>().Single();

        Assert.Equal("A \"quoted\" name", record.GetType().GetProperty("FirstName")!.GetValue(record));
    }

    [Fact]
    public void Csv_parser_skips_rows_with_fewer_than_four_fields_or_blank_identity()
    {
        var method = typeof(BulkImportEndpoints).GetMethod("ParseCsv", BindingFlags.NonPublic | BindingFlags.Static)!;
        const string csv = "email,username,firstname,lastname\nonly@example.com,only\n,blank,First,Last\nvalid@example.com,valid,First,Last\n";

        var records = ((System.Collections.IEnumerable)method.Invoke(null, [csv])!).Cast<object>().ToArray();

        Assert.Single(records);
        Assert.Equal("valid@example.com", records[0].GetType().GetProperty("Email")!.GetValue(records[0]));
    }

    [Fact]
    public void Xlsx_parser_reads_inline_strings_and_shared_strings()
    {
        var method = typeof(BulkImportEndpoints).GetMethod("ParseXlsx", BindingFlags.NonPublic | BindingFlags.Static)!;
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var shared = archive.CreateEntry("xl/sharedStrings.xml");
            using (var writer = new StreamWriter(shared.Open()))
                writer.Write("<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><si><t>user@example.com</t></si></sst>");

            var sheet = archive.CreateEntry("xl/worksheets/sheet1.xml");
            using (var writer = new StreamWriter(sheet.Open()))
                writer.Write("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row><c><v>email</v></c><c><v>username</v></c><c><v>firstname</v></c><c><v>lastname</v></c></row><row><c t=\"s\"><v>0</v></c><c><is><t>inline-user</t></is></c><c><is><t>First</t></is></c><c><is><t>Last</t></is></c></row></sheetData></worksheet>");
        }

        var records = ((System.Collections.IEnumerable)method.Invoke(null, [stream.ToArray()])!).Cast<object>().ToArray();

        Assert.Single(records);
        Assert.Equal("user@example.com", records[0].GetType().GetProperty("Email")!.GetValue(records[0]));
        Assert.Equal("inline-user", records[0].GetType().GetProperty("UserName")!.GetValue(records[0]));
    }

    [Fact]
    public void Xlsx_parser_requires_the_first_worksheet()
    {
        var method = typeof(BulkImportEndpoints).GetMethod("ParseXlsx", BindingFlags.NonPublic | BindingFlags.Static)!;
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            archive.CreateEntry("xl/workbook.xml");

        var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [stream.ToArray()]));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Contains("first worksheet", error.InnerException!.Message);
    }

    [Fact]
    public async Task Import_parser_rejects_oversized_http_payload()
    {
        var method = typeof(BulkImportEndpoints).GetMethod("ParseImportAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var context = new DefaultHttpContext();
        context.Request.ContentLength = 10 * 1024 * 1024 + 1;
        var invocation = (Task)method.Invoke(null, [context.Request, CancellationToken.None])!;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await invocation);
        Assert.Contains("10 MB", error.Message);
    }
}
