using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using His.Hope.Authorization;
using His.Hope.IdentityService.Api.Jobs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.IdentityService.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class OpenFgaPermissionProjectionTests
{
    [Fact]
    public async Task Real_OpenFga_model_enforces_authentication_grants_revocation_and_no_wildcards()
    {
        await using var container = new ContainerBuilder(
                "openfga/openfga:v1.22.1@sha256:b852071314f1fda1194d341a6ebac0a4f619b3cd87a6626d2ccc7048af4ec671")
            .WithCommand("run")
            .WithEnvironment("OPENFGA_DATASTORE_ENGINE", "memory")
            .WithEnvironment("OPENFGA_AUTHN_METHOD", "preshared")
            .WithEnvironment("OPENFGA_AUTHN_PRESHARED_KEYS", "projection-test-token")
            .WithEnvironment("OPENFGA_PLAYGROUND_ENABLED", "false")
            .WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
                request.ForPort(8080).ForPath("/healthz")))
            .Build();
        await container.StartAsync();
        var endpoint = new UriBuilder("http", container.Hostname, container.GetMappedPublicPort(8080)).Uri;
        using var bootstrap = new HttpClient { BaseAddress = endpoint };
        using (var unauthenticated = await bootstrap.GetAsync("stores"))
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        bootstrap.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "projection-test-token");
        using var createdStore = await bootstrap.PostAsJsonAsync("stores",
            new { name = OpenFgaPermissionProjection.StoreName });
        createdStore.EnsureSuccessStatusCode();
        var store = await createdStore.Content.ReadFromJsonAsync<JsonElement>();
        var storeId = store.GetProperty("id").GetString()!;
        var modelJson = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "OpenFgaPermissionModel.json"));
        using var createdModel = await bootstrap.PostAsync($"stores/{storeId}/authorization-models",
            new StringContent(modelJson, Encoding.UTF8, "application/json"));
        createdModel.EnsureSuccessStatusCode();
        var model = await createdModel.Content.ReadFromJsonAsync<JsonElement>();
        var configuration = PermissionProjectionHandler.Configuration();
        configuration["AUTHZ_OPENFGA_URL"] = endpoint.AbsoluteUri;
        configuration["AUTHZ_OPENFGA_STORE_ID"] = storeId;
        configuration["AUTHZ_OPENFGA_MODEL_ID"] = model.GetProperty("authorization_model_id").GetString();
        configuration["AUTHZ_PDP_MODE"] = "canary";
        using var projectionHttp = new HttpClient();
        var projection = new OpenFgaPermissionProjection(projectionHttp, configuration);
        var subject = Guid.NewGuid();
        var desired = Enumerable.Range(0, 205)
            .Select(index => Grant(subject, $"registered-service.permission-{index}")).ToHashSet();
        desired.Add(Grant(subject, "admin.clients.read"));
        Assert.Equal(new OpenFgaPermissionProjectionResult(0, 206), await projection.ReconcileAsync(desired));
        Assert.Equal(new OpenFgaPermissionProjectionResult(0, 0), await projection.ReconcileAsync(desired));
        using var checkHttp = new HttpClient { BaseAddress = endpoint, Timeout = TimeSpan.FromMilliseconds(500) };
        var client = new OpenFgaClient(checkHttp, configuration);
        var authorizer = new OpenFgaCanaryAuthorizer(
            client, NullLogger<OpenFgaCanaryAuthorizer>.Instance, configuration);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject.ToString("D"))], "Bearer"));
        Assert.True(await authorizer.AllowsAsync(principal, "admin.clients.read"));
        Assert.False(await client.CheckAsync($"user:{Guid.NewGuid():D}", "granted", "permission:admin.clients.read"));
        using (var wildcard = await bootstrap.PostAsJsonAsync($"stores/{storeId}/write", new
        {
            authorization_model_id = configuration["AUTHZ_OPENFGA_MODEL_ID"],
            writes = new { tuple_keys = new[] { new { user = "user:*", relation = "granted", @object = "permission:admin.clients.read" } } }
        }))
            Assert.Equal(HttpStatusCode.BadRequest, wildcard.StatusCode);
        Assert.Equal(new OpenFgaPermissionProjectionResult(206, 0),
            await projection.ReconcileAsync(new HashSet<OpenFgaPermissionTuple>()));
        Assert.False(await authorizer.AllowsAsync(principal, "admin.clients.read"));
    }

    [Fact]
    public async Task Reconciles_revocations_before_grants_and_is_idempotent()
    {
        using var server = new PermissionProjectionHandler();
        var old = Grant(Guid.NewGuid(), "admin.users.read");
        var kept = Grant(Guid.NewGuid(), "admin.clients.read");
        var added = Grant(Guid.NewGuid(), "registered-service.read");
        server.Tuples.UnionWith([old, kept]);
        using var http = new HttpClient(server);
        var projection = new OpenFgaPermissionProjection(http, PermissionProjectionHandler.Configuration());
        var desired = new HashSet<OpenFgaPermissionTuple> { kept, added };

        Assert.Equal(new OpenFgaPermissionProjectionResult(1, 1),
            await projection.ReconcileAsync(desired));
        Assert.Equal(["deletes", "writes"], server.Mutations.Select(item => item.Operation));
        Assert.True(server.Tuples.SetEquals(desired));
        Assert.Equal(new OpenFgaPermissionProjectionResult(0, 0),
            await projection.ReconcileAsync(desired));
        Assert.Equal(2, server.Mutations.Count);
    }

    [Fact]
    public async Task Reads_every_page_and_limits_each_write_to_one_hundred_tuples()
    {
        using var server = new PermissionProjectionHandler();
        server.Tuples.UnionWith(Enumerable.Range(0, 250)
            .Select(index => Grant(Guid.NewGuid(), $"old-service.permission-{index}")));
        var desired = Enumerable.Range(0, 255)
            .Select(index => Grant(Guid.NewGuid(), $"new-service.permission-{index}")).ToHashSet();
        using var http = new HttpClient(server);
        var projection = new OpenFgaPermissionProjection(http, PermissionProjectionHandler.Configuration());

        Assert.Equal(new OpenFgaPermissionProjectionResult(250, 255),
            await projection.ReconcileAsync(desired));
        Assert.Equal(3, server.ReadPages);
        Assert.Equal([100, 100, 50, 100, 100, 55], server.Mutations.Select(item => item.Count));
        Assert.Equal(["deletes", "deletes", "deletes", "writes", "writes", "writes"],
            server.Mutations.Select(item => item.Operation));
        Assert.True(server.Tuples.SetEquals(desired));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Refuses_an_unowned_store_or_unmanaged_tuples(bool wrongStore)
    {
        using var server = new PermissionProjectionHandler();
        if (wrongStore)
            server.StoreName = "another-service-store";
        else
            server.Tuples.Add(new OpenFgaPermissionTuple("user:*", "viewer", "document:private"));
        using var http = new HttpClient(server);
        var projection = new OpenFgaPermissionProjection(http, PermissionProjectionHandler.Configuration());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            projection.ReconcileAsync(new HashSet<OpenFgaPermissionTuple>()));
        Assert.Empty(server.Mutations);
    }

    [Fact]
    public async Task Does_not_add_grants_when_revocation_fails()
    {
        using var server = new PermissionProjectionHandler { FailDeletes = true };
        var old = Grant(Guid.NewGuid(), "admin.users.read");
        server.Tuples.Add(old);
        using var http = new HttpClient(server);
        var projection = new OpenFgaPermissionProjection(http, PermissionProjectionHandler.Configuration());

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            projection.ReconcileAsync(new HashSet<OpenFgaPermissionTuple>
            {
                Grant(Guid.NewGuid(), "admin.clients.read")
            }));
        Assert.Single(server.Mutations);
        Assert.Equal("deletes", server.Mutations[0].Operation);
        Assert.True(server.Tuples.SetEquals([old]));
    }

    [Theory]
    [InlineData("AUTHZ_OPENFGA_URL")]
    [InlineData("AUTHZ_OPENFGA_TOKEN")]
    [InlineData("AUTHZ_OPENFGA_STORE_ID")]
    [InlineData("AUTHZ_OPENFGA_MODEL_ID")]
    [InlineData("AUTHZ_OPENFGA_PERMISSION_RELATION")]
    public void Requires_explicit_projection_configuration(string key)
    {
        var configuration = PermissionProjectionHandler.Configuration();
        configuration[key] = "";
        using var http = new HttpClient();

        Assert.Throws<InvalidOperationException>(() =>
            new OpenFgaPermissionProjection(http, configuration));
    }

    internal static OpenFgaPermissionTuple Grant(Guid user, string permission) =>
        new($"user:{user:D}", OpenFgaPermissionProjection.Relation, $"permission:{permission}");
}

[Collection("IdentityServiceIntegration")]
public sealed class OpenFgaPermissionSyncWorkerTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task Projects_canonical_permissions_and_revokes_inactive_and_locked_users()
    {
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var inactive = new User
        {
            Id = Guid.NewGuid(), UserName = $"fga-inactive-{Guid.NewGuid():N}",
            Email = $"fga-inactive-{Guid.NewGuid():N}@example.invalid", EmailConfirmed = true, IsActive = false
        };
        var locked = new User
        {
            Id = Guid.NewGuid(), UserName = $"fga-locked-{Guid.NewGuid():N}",
            Email = $"fga-locked-{Guid.NewGuid():N}@example.invalid", EmailConfirmed = true,
            LockoutEnd = DateTime.UtcNow.AddHours(1)
        };
        Assert.True((await users.CreateAsync(inactive)).Succeeded);
        Assert.True((await users.CreateAsync(locked)).Succeeded);
        try
        {
            Assert.True((await users.AddToRoleAsync(inactive, "Admin")).Succeeded);
            Assert.True((await users.AddToRoleAsync(locked, "Admin")).Succeeded);
            using var server = new PermissionProjectionHandler();
            server.Tuples.UnionWith([
                OpenFgaPermissionProjectionTests.Grant(inactive.Id, "admin.clients.read"),
                OpenFgaPermissionProjectionTests.Grant(locked.Id, "admin.clients.read")
            ]);
            var httpClients = new Mock<IHttpClientFactory>();
            httpClients.Setup(factory => factory.CreateClient(OpenFgaPermissionProjection.HttpClientName))
                .Returns(() => new HttpClient(server, disposeHandler: false));
            var worker = new OpenFgaPermissionSyncWorker(
                fixture.Services.GetRequiredService<IServiceScopeFactory>(),
                fixture.Services.GetRequiredService<IdentityRedisLock>(),
                httpClients.Object, PermissionProjectionHandler.Configuration(),
                NullLogger<OpenFgaPermissionSyncWorker>.Instance);

            await worker.ReconcileAsync(CancellationToken.None);

            Assert.DoesNotContain(server.Tuples, tuple =>
                tuple.User == $"user:{inactive.Id:D}" || tuple.User == $"user:{locked.Id:D}");
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            var expected = (await identity.GetEffectivePermissionsAsync(IdentityTestData.AdminId))
                .Select(permission => OpenFgaPermissionProjectionTests.Grant(IdentityTestData.AdminId, permission))
                .ToHashSet();
            Assert.NotEmpty(expected);
            Assert.True(expected.SetEquals(server.Tuples.Where(tuple =>
                tuple.User == $"user:{IdentityTestData.AdminId:D}")));
        }
        finally
        {
            Assert.True((await users.DeleteAsync(inactive)).Succeeded);
            Assert.True((await users.DeleteAsync(locked)).Succeeded);
        }
    }
}

internal sealed class PermissionProjectionHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public HashSet<OpenFgaPermissionTuple> Tuples { get; } = [];
    public List<(string Operation, int Count)> Mutations { get; } = [];
    public string StoreName { get; set; } = OpenFgaPermissionProjection.StoreName;
    public bool FailDeletes { get; set; }
    public int ReadPages { get; private set; }

    internal static IConfigurationRoot Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AUTHZ_OPENFGA_URL"] = "http://openfga.test/",
            ["AUTHZ_OPENFGA_TOKEN"] = "projection-test-token",
            ["AUTHZ_OPENFGA_STORE_ID"] = "owned-store",
            ["AUTHZ_OPENFGA_MODEL_ID"] = "permission-model",
            ["AUTHZ_OPENFGA_PERMISSION_RELATION"] = "granted"
        }).Build();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("projection-test-token", request.Headers.Authorization?.Parameter);
        if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/stores/owned-store")
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { name = StoreName }) };

        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        if (request.RequestUri?.AbsolutePath == "/stores/owned-store/read")
        {
            Assert.Equal(100, body.RootElement.GetProperty("page_size").GetInt32());
            var token = body.RootElement.GetProperty("continuation_token").GetString();
            var offset = string.IsNullOrEmpty(token) ? 0 : int.Parse(token);
            var all = Tuples.ToArray();
            var page = all.Skip(offset).Take(100).Select(key => new { key }).ToArray();
            ReadPages++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    tuples = page,
                    continuation_token = offset + page.Length < all.Length ? (offset + page.Length).ToString() : ""
                })
            };
        }

        Assert.Equal("/stores/owned-store/write", request.RequestUri?.AbsolutePath);
        Assert.Equal("permission-model", body.RootElement.GetProperty("authorization_model_id").GetString());
        var operation = body.RootElement.TryGetProperty("deletes", out var mutation) ? "deletes" : "writes";
        if (operation == "writes")
            mutation = body.RootElement.GetProperty("writes");
        var tuples = mutation.GetProperty("tuple_keys").Deserialize<OpenFgaPermissionTuple[]>(JsonOptions)!;
        Assert.InRange(tuples.Length, 1, 100);
        Mutations.Add((operation, tuples.Length));
        if (operation == "deletes" && FailDeletes)
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        foreach (var tuple in tuples)
        {
            if (operation == "deletes") Tuples.Remove(tuple);
            else Tuples.Add(tuple);
        }
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }
}
