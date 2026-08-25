using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using His.Hope.IdentityService.Api.Endpoints;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class HrWebhookEndpointTests(IdentityServiceTestFixture fixture)
{
    private const string TestWebhookKey = "test-webhook-signing-key-for-tests";
    private const string Route = "/api/v1/webhook/hr";

    [Fact]
    public async Task Hired_event_provisions_user_and_maps_department_role()
    {
        var eventId = $"hired-{Guid.NewGuid():N}";
        var body = JsonSerializer.Serialize(new { eventType = "employee.hired", eventId, timestamp = "2026-07-23T00:00:00Z", employee = new { employeeId = $"employee-{eventId}", email = $"{eventId}@example.test", firstName = "Ada", lastName = "Lovelace", department = "nursing", facilityId = "facility-hq" } });

        var response = await SendSignedAsync(body, eventId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<HrWebhookResponse>();
        Assert.NotNull(result);
        Assert.Equal("provisioned", result.Status);
        Assert.StartsWith("employee-", result.EmployeeId, StringComparison.Ordinal);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Updated_event_provisions_user_with_case_insensitive_json_names()
    {
        var eventId = $"updated-{Guid.NewGuid():N}";
        var body = $"{{\"EVENTTYPE\":\"employee.updated\",\"EVENTID\":\"{eventId}\",\"timestamp\":\"2026-07-23T00:00:00Z\",\"employee\":{{\"employeeId\":\"employee-{eventId}\",\"email\":\"{eventId}@example.test\",\"department\":\"LABORATORY\"}}}}";

        var response = await SendSignedAsync(body, eventId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<HrWebhookResponse>();
        Assert.NotNull(result);
        Assert.Equal("provisioned", result.Status);
    }

    [Fact]
    public async Task Terminated_event_is_acknowledged_without_importing()
    {
        var eventId = $"terminated-{Guid.NewGuid():N}";
        var body = JsonSerializer.Serialize(new { eventType = "employee.terminated", eventId, timestamp = "2026-07-23T00:00:00Z", employee = new { employeeId = $"employee-{eventId}", email = $"{eventId}@example.test" } });

        var response = await SendSignedAsync(body, eventId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<HrWebhookResponse>();
        Assert.NotNull(result);
        Assert.Equal("acknowledged", result.Status);
        Assert.Equal("User deactivation handled via SCIM PATCH", result.Error);
    }

    [Fact]
    public async Task Unsupported_event_type_returns_structured_problem()
    {
        var eventId = $"unsupported-{Guid.NewGuid():N}";
        var body = JsonSerializer.Serialize(new { eventType = "employee.promoted", eventId, timestamp = "2026-07-23T00:00:00Z", employee = new { employeeId = $"employee-{eventId}" } });

        var response = await SendSignedAsync(body, eventId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadAsStringAsync();
        Assert.Contains("unsupported_hr_event_type", problem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Event_id_mismatch_is_rejected_before_processing()
    {
        var headerEventId = $"header-{Guid.NewGuid():N}";
        var bodyEventId = $"body-{Guid.NewGuid():N}";
        var body = JsonSerializer.Serialize(new { eventType = "employee.terminated", eventId = bodyEventId, timestamp = "2026-07-23T00:00:00Z", employee = new { employeeId = $"employee-{bodyEventId}" } });

        var response = await SendSignedAsync(body, headerEventId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadAsStringAsync();
        Assert.Contains("hr_webhook_event_mismatch", problem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invalid_json_returns_invalid_payload_problem()
    {
        var eventId = $"invalid-json-{Guid.NewGuid():N}";
        const string body = "{not-json";

        var response = await SendSignedAsync(body, eventId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_hr_webhook_payload", problem, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HttpResponseMessage> SendSignedAsync(string body, string headerEventId)
    {
        var configuration = fixture.Services.GetRequiredService<IConfiguration>();
        configuration["HrWebhook:Secret"] = TestWebhookKey;
        configuration["HrWebhook:TimestampToleranceSeconds"] = "300";

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Post, Route)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add(HrWebhookAuthenticator.TimestampHeader, timestamp);
        request.Headers.Add(HrWebhookAuthenticator.EventIdHeader, headerEventId);
        request.Headers.Add(
            HrWebhookAuthenticator.SignatureHeader,
            HrWebhookAuthenticator.ComputeSignature(TestWebhookKey, timestamp, body));

        return await fixture.AnonymousClient.SendAsync(request);
    }
}
