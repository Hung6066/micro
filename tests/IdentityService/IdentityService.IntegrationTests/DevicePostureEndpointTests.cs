using System.Net;
using System.Net.Http.Json;
using His.Hope.IdentityService.Testing;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class DevicePostureEndpointTests
{
    private readonly IdentityServiceTestFixture _fixture;

    public DevicePostureEndpointTests(IdentityServiceTestFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(IdentityApiRoutes.AdminDevicePosturePolicy)]
    [InlineData(IdentityApiRoutes.AdminDevicePostureAssessments)]
    [InlineData(IdentityApiRoutes.AdminDevicePosturePreview)]
    public async Task AdminPostureEndpoints_RequirePermission(string path)
    {
        var response = path.EndsWith("preview", StringComparison.Ordinal)
            ? await _fixture.AnonymousClient.PostAsJsonAsync(path, new
            {
                userId = Guid.NewGuid(),
                deviceId = "pilot-device",
                provider = "advanced-compliance",
                signals = new { managed = true },
                observedAt = DateTime.UtcNow
            })
            : await _fixture.AnonymousClient.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DecisionEndpoint_RequiresAuthentication()
    {
        var response = await _fixture.AnonymousClient.GetAsync(
            IdentityApiRoutes.DevicePostureDecisionFor(Guid.NewGuid(), "pilot-device"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_policy_preview_and_update_validate_contract()
    {
        using var session = await LoginAsync();

        Assert.Equal(HttpStatusCode.OK,
            (await session.GetWithCookiesAsync(IdentityApiRoutes.AdminDevicePosturePolicy)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PutWithCookiesAsync(IdentityApiRoutes.AdminDevicePosturePolicy, new
            {
                mode = "invalid",
                providers = new[] { "advanced-compliance" },
                evidenceTtlSeconds = 900,
                requiredSignals = Array.Empty<string>()
            })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PutWithCookiesAsync(IdentityApiRoutes.AdminDevicePosturePolicy, new
            {
                mode = "observe",
                providers = new[] { "advanced-compliance" },
                evidenceTtlSeconds = 30,
                requiredSignals = Array.Empty<string>()
            })).StatusCode);

        var update = await session.PutWithCookiesAsync(IdentityApiRoutes.AdminDevicePosturePolicy, new
        {
            mode = "stepup",
            providers = new[] { "advanced-compliance" },
            evidenceTtlSeconds = 900,
            requiredSignals = new[] { "managed" }
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var preview = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminDevicePosturePreview, new
        {
            userId = IdentityTestData.AdminId,
            deviceId = $"device-{Guid.NewGuid():N}",
            provider = "advanced-compliance",
            signals = new Dictionary<string, bool> { ["managed"] = true },
            observedAt = DateTime.UtcNow
        });
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminDevicePosturePolicy}/rollback", new { })).StatusCode);
    }

    [Fact]
    public async Task Assessment_is_accepted_replay_is_conflict_and_decision_is_visible()
    {
        using var session = await LoginAsync();
        var userId = IdentityTestData.AdminId;
        var deviceId = $"assessment-device-{Guid.NewGuid():N}";
        var evidence = new
        {
            userId,
            deviceId,
            provider = "advanced-compliance",
            signals = new Dictionary<string, bool> { ["managed"] = true },
            observedAt = DateTime.UtcNow,
            replayNonce = Guid.NewGuid().ToString("N")
        };

        var accepted = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminDevicePostureAssessments, evidence);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminDevicePostureAssessments, evidence)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.GetWithCookiesAsync(IdentityApiRoutes.AdminDevicePostureAssessments)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.GetWithCookiesAsync(IdentityApiRoutes.DevicePostureDecisionFor(userId, deviceId))).StatusCode);
    }

    private async Task<SessionClient> LoginAsync()
    {
        var session = _fixture.CreateSessionClient();
        var response = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return session;
    }
}
