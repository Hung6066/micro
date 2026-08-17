extern alias IdentityApi;

using System.Security.Claims;
using System.Reflection;
using Grpc.Core;
using His.Hope.Authorization.Requirements;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Api.Controllers;
using His.Hope.IdentityService.Api.Jobs;
using His.Hope.IdentityService.Api.Services;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class ApiLowCoverageTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task Grpc_permission_handler_grants_authenticated_user_when_grpc_allows()
    {
        var client = new Mock<IdentityApi::His.Hope.Identity.Grpc.IdentityService.IdentityServiceClient>();
        client.Setup(x => x.CheckPermissionAsync(
                It.Is<IdentityApi::His.Hope.Identity.Grpc.CheckPermissionRequest>(r => r.UserId == "user-1" && r.PermissionCode == "users.read"),
                It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<IdentityApi::His.Hope.Identity.Grpc.CheckPermissionResponse>(
                Task.FromResult(new IdentityApi::His.Hope.Identity.Grpc.CheckPermissionResponse { HasPermission = true }),
                Task.FromResult(new Metadata()),
                () => new Status(StatusCode.OK, ""),
                () => new Metadata(),
                () => { }));

        var handler = new IdentityApi::His.Hope.IdentityService.Api.Authorization.GrpcPermissionHandler(client.Object, NullLogger<IdentityApi::His.Hope.IdentityService.Api.Authorization.GrpcPermissionHandler>.Instance);
        var requirement = new PermissionRequirement("users.read");
        var context = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-1")], "test")),
            null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        client.VerifyAll();
    }

    [Fact]
    public async Task Grpc_permission_handler_falls_back_to_csv_claim_when_grpc_fails()
    {
        var client = new Mock<IdentityApi::His.Hope.Identity.Grpc.IdentityService.IdentityServiceClient>();
        client.Setup(x => x.CheckPermissionAsync(
                It.IsAny<IdentityApi::His.Hope.Identity.Grpc.CheckPermissionRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "offline")));

        var handler = new IdentityApi::His.Hope.IdentityService.Api.Authorization.GrpcPermissionHandler(client.Object, NullLogger<IdentityApi::His.Hope.IdentityService.Api.Authorization.GrpcPermissionHandler>.Instance);
        var requirement = new PermissionRequirement("users.read");
        var context = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, "user-2"),
                new Claim("permissions", "users.write, users.read")
            ], "test")),
            null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Grpc_permission_handler_denies_anonymous_or_missing_subject()
    {
        var client = new Mock<IdentityApi::His.Hope.Identity.Grpc.IdentityService.IdentityServiceClient>(MockBehavior.Strict);
        var handler = new IdentityApi::His.Hope.IdentityService.Api.Authorization.GrpcPermissionHandler(client.Object, NullLogger<IdentityApi::His.Hope.IdentityService.Api.Authorization.GrpcPermissionHandler>.Instance);
        var requirement = new PermissionRequirement("users.read");

        var anonymous = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(new ClaimsIdentity()), null);
        await handler.HandleAsync(anonymous);
        Assert.False(anonymous.HasSucceeded);

        var noSubject = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity([], "test")),
            null);
        await handler.HandleAsync(noSubject);
        Assert.False(noSubject.HasSucceeded);
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Saml_login_returns_not_found_when_federation_is_not_configured()
    {
        using var scope = fixture.Services.CreateScope();
        var controller = new SamlFederationController(
            scope.ServiceProvider.GetRequiredService<SamlRuntimeConfigurationService>(),
            scope.ServiceProvider.GetRequiredService<UserManager<User>>(),
            scope.ServiceProvider.GetRequiredService<RoleManager<Role>>(),
            scope.ServiceProvider.GetRequiredService<OidcLoginCompletionService>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider }
        };

        var result = await controller.Login("/account");

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task Admin_job_worker_starts_and_stops_cleanly_on_cancellation()
    {
        var worker = new AdminJobWorker(
            new RedisAdminJobStore(fixture.Services.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>()),
            fixture.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AdminJobWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token);
    }

    [Fact]
    public async Task Admin_job_worker_ignores_missing_job_state()
    {
        var worker = new AdminJobWorker(
            new RedisAdminJobStore(fixture.Services.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>()),
            fixture.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AdminJobWorker>.Instance);
        var process = typeof(AdminJobWorker).GetMethod("ProcessAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var task = (Task)process.Invoke(worker, [Guid.NewGuid().ToString("N"), CancellationToken.None])!;
        await task;
    }

    [Fact]
    public void Identity_support_helpers_normalize_cookie_domain_and_hash_values()
    {
        var assembly = typeof(SamlFederationController).Assembly;
        var helpers = assembly.GetType("His.Hope.IdentityService.Api.Composition.BffHelpers", throwOnError: true)!;
        const BindingFlags NonPublicStatic = BindingFlags.NonPublic | BindingFlags.Static;
        var cookieDomain = helpers.GetMethod("CookieDomain", NonPublicStatic)!;
        var hash = helpers.GetMethod("ComputeSha256", NonPublicStatic)!;

        var empty = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:CookieDomain"] = "  "
        }).Build();
        Assert.Null(cookieDomain.Invoke(null, [empty]));

        var configured = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:CookieDomain"] = " .example.test "
        }).Build();
        Assert.Equal(".example.test", cookieDomain.Invoke(null, [configured]));
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", hash.Invoke(null, [""]));
    }

    [Fact]
    public async Task Identity_database_health_check_fails_closed_when_connection_is_unavailable()
    {
        var type = typeof(SamlFederationController).Assembly.GetType(
            "His.Hope.IdentityService.Api.Composition.DbHealthCheck", throwOnError: true)!;
        var check = Activator.CreateInstance(type, ["Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing", null])!;
        var method = type.GetMethod("CheckHealthAsync")!;
        var task = (Task)method.Invoke(check, [new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext(), CancellationToken.None])!;
        await task;
        var result = (Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult)task.GetType().GetProperty("Result")!.GetValue(task)!;
        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public void Saml_controller_helpers_validate_issuer_claims_and_local_return_urls()
    {
        var type = typeof(SamlFederationController);
        var buildAcs = type.GetMethod("BuildAcsUrl", BindingFlags.Static | BindingFlags.NonPublic)!;
        var findClaim = type.GetMethod("FindClaim", BindingFlags.Static | BindingFlags.NonPublic)!;
        var safeReturn = type.GetMethod("IsSafeLocalReturnUrl", BindingFlags.Static | BindingFlags.NonPublic)!;

        var uri = (Uri)buildAcs.Invoke(null, ["https://issuer.example.test/base"])!;
        Assert.Equal("https://issuer.example.test/api/v1/federation/saml/acs", uri.AbsoluteUri);
        Assert.Throws<TargetInvocationException>(() => buildAcs.Invoke(null, ["not-an-absolute-url"]));

        var identity = new ClaimsIdentity([new Claim("mail", "user@example.test")]);
        Assert.Equal("user@example.test", findClaim.Invoke(null, [identity, new[] { "email", "mail" }]));
        Assert.Null(findClaim.Invoke(null, [identity, new[] { "missing" }]));

        Assert.True((bool)safeReturn.Invoke(null, ["/account"])!);
        Assert.False((bool)safeReturn.Invoke(null, ["//evil.example"])!);
        Assert.False((bool)safeReturn.Invoke(null, ["/\\evil"])!);
        Assert.False((bool)safeReturn.Invoke(null, ["/https:evil"])!);
        Assert.False((bool)safeReturn.Invoke(null, [null])!);
    }

    [Fact]
    public async Task Production_configuration_validator_rejects_missing_security_configuration()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        await using var app = builder.Build();
        var type = typeof(SamlFederationController).Assembly.GetType(
            "His.Hope.IdentityService.Api.Composition.ProductionConfigurationValidator", throwOnError: true)!;
        var validate = type.GetMethod("Validate", BindingFlags.Static | BindingFlags.Public)!;

        var error = Assert.Throws<TargetInvocationException>(() => validate.Invoke(null, [app]));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Contains("critical configuration error", error.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Production_configuration_validator_accepts_complete_https_configuration()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Vault:Address"] = "https://vault.example.test",
            ["Vault:EnableTransit"] = "true",
            ["Vault:Role"] = "identity",
            ["Vault:JwtTokenFile"] = "C:/var/run/secrets/vault/jwt",
            ["OpenIddict:Issuer"] = "https://identity.example.test",
            ["ConnectionStrings:Redis"] = "redis:6379",
            ["OpenIddict:AllowInsecureHttp"] = "false"
        });
        await using var app = builder.Build();
        var type = typeof(SamlFederationController).Assembly.GetType(
            "His.Hope.IdentityService.Api.Composition.ProductionConfigurationValidator", throwOnError: true)!;
        var validate = type.GetMethod("Validate", BindingFlags.Static | BindingFlags.Public)!;

        validate.Invoke(null, [app]);
    }
}
