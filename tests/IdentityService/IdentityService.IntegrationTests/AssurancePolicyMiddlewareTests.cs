using System.Security.Claims;
using System.Net.Http;
using System.Text.Json;
using His.Hope.AspNetCore;
using His.Hope.IdentityService.Api.Middleware;
using His.Hope.IdentityService.Application.Assurance;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class AssurancePolicyMiddlewareTests
{
    [Fact]
    public async Task Production_denial_preserves_step_up_reason_through_error_contract()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseEnvironment("Production")
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddHisHopeAspNetCore();
                    services.AddSingleton(Policy());
                })
                .Configure(app =>
                {
                    app.UseHisHopeAspNetCore();
                    app.Use(async (context, next) =>
                    {
                        context.User = Context(HttpMethods.Post, "/api/v1/admin/clients", "pwd").User;
                        await next();
                    });
                    app.UseMiddleware<AssurancePolicyMiddleware>();
                    app.Run(_ => Task.CompletedTask);
                }))
            .StartAsync();

        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "assurance-regression");
        var response = await client.PostAsync("/api/v1/admin/clients", new StringContent("{}"));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement;
        Assert.Equal("assurance_policy_denied", body.GetProperty("errorCode").GetString());
        Assert.Equal("admin-write", body.GetProperty("journey").GetString());
        Assert.Equal("Assurance below journey minimum.", body.GetProperty("reason").GetString());
        Assert.Equal("assurance-regression", body.GetProperty("correlationId").GetString());
    }

    private static AssurancePolicyService Policy(bool allowBreakGlass = false) =>
        new(new AssurancePolicyDocument(
            "v1", ["security"],
            [
                new AssuranceJourneyPolicy("admin-write", "aal2", ["mfa"], false),
                new AssuranceJourneyPolicy("clinical-read", "aal2", ["mfa"], false),
                new AssuranceJourneyPolicy("break-glass", "aal3", ["mtls"], false, AllowBreakGlass: allowBreakGlass)
            ],
            new AssuranceRecoveryPolicy(true, ["passkey"], [])));

    [Fact]
    public async Task Testing_environment_bypasses_enforcement()
    {
        var called = false;
        var middleware = new AssurancePolicyMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }, Environment("Testing"));
        var context = Context(HttpMethods.Post, "/api/v1/admin/users", "pwd");

        await middleware.InvokeAsync(context, Policy());

        Assert.True(called);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_and_safe_methods_continue_without_policy_evaluation()
    {
        var called = false;
        var middleware = new AssurancePolicyMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }, Environment("Production"));

        await middleware.InvokeAsync(Context(HttpMethods.Get, "/api/v1/admin/users"), Policy());
        Assert.True(called);
        called = false;
        await middleware.InvokeAsync(Context(HttpMethods.Post, "/api/v1/health", "pwd"), Policy());
        Assert.True(called);
    }

    [Fact]
    public async Task Insufficient_assurance_is_denied_for_admin_write()
    {
        var middleware = new AssurancePolicyMiddleware(_ => Task.CompletedTask, Environment("Production"));
        var context = Context(HttpMethods.Post, "/api/v1/admin/users", "pwd");

        await middleware.InvokeAsync(context, Policy());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Strong_assurance_allows_clinical_and_break_glass_paths()
    {
        var called = 0;
        var middleware = new AssurancePolicyMiddleware(_ =>
        {
            called++;
            return Task.CompletedTask;
        }, Environment("Production"));

        await middleware.InvokeAsync(Context(HttpMethods.Post, "/api/v1/clinical/orders", "mfa"), Policy());
        await middleware.InvokeAsync(Context(HttpMethods.Post, "/api/v1/break-glass/approve", "mtls"), Policy(true));

        Assert.Equal(2, called);
    }

    private static DefaultHttpContext Context(string method, string path, string? amr = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        if (amr is not null)
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "user-1"), new Claim("amr", amr)], "test"));
        return context;
    }

    private static IHostEnvironment Environment(string name)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(item => item.EnvironmentName).Returns(name);
        return environment.Object;
    }
}
