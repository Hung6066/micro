using System.Net;
using His.Hope.Contracts.Identity;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class BulkImportEndpointTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task Bulk_import_endpoints_reject_empty_and_malformed_payloads()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();

        var emptyJson = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminUsersBulk, new
        {
            users = Array.Empty<object>(),
            sendWelcomeEmail = false,
            skipExisting = true
        });
        Assert.Equal(HttpStatusCode.BadRequest, emptyJson.StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminUsersBulkCsv, "")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminUsersBulkFile, "")).StatusCode);
        var preview = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminUsersBulkPreview, "");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
    }
}
