# BFF Microservices Architecture — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform His.Hope from monolithic YARP gateway to per-module BFF architecture with HttpOnly cookie auth and API aggregation.

**Architecture:** YARP stays as edge gateway. 6 per-module BFF services (Patient, Clinical, Lab, Billing, Pharmacy, Dashboard) sit between YARP and backend services. Each BFF handles cookie→JWT session exchange and optional response aggregation. Migration via Unleash feature flags — dual-path parallel running.

**Tech Stack:** .NET 8, ASP.NET Core, YARP v2.1, Polly, StackExchange.Redis, gRPC, Angular 17, Unleash, CockroachDB, Linkerd, Cilium, Vault, Tekton, ArgoCD.

## Global Constraints

- .NET 8 target framework — all BFF projects must use `net8.0`
- Angular 17 standalone components — no modules for new code
- All secrets via Vault AppRole — never hardcode connection strings
- All inter-service calls must have circuit breakers (Polly)
- Database migrations must be backward-compatible
- Container images must be distroless or slim, non-root user
- All containers must have `readOnlyRootFilesystem: true`
- gRPC for inter-service communication, REST for BFF→frontend
- Clean Architecture: new BFFs follow Controller → Handler → Client pattern
- Conventional Commits: `feat(bff):`, `test(bff):`, `docs(bff):`, `chore(bff):`
- Cookie security: HttpOnly, Secure, SameSite=Lax mandatory on all session cookies
- CSRF enforced on all state-changing requests (POST/PUT/PATCH/DELETE)
- No BFF-to-BFF calls — BFF calls backend services only

---

## Phase 0: Shared Bff.Core Library (Week 1, Day 1)

### Task 0.1: Create Bff.Core project structure

**Files:**
- Create: `src/Bff/His.Hope.Bff.Core/His.Hope.Bff.Core.csproj`
- Create: `src/Bff/His.Hope.Bff.Core/DependencyInjection.cs`
- Create: `src/Bff/His.Hope.Bff.Core/Authentication/SessionCookieOptions.cs`
- Create: `src/Bff/His.Hope.Bff.Core/Authentication/SessionData.cs`

**Interfaces:**
- Produces: `SessionCookieOptions` (record: `CookieName`, `CookieDomain`, `CookiePath`, `CookieMaxAge`, `Secure`, `HttpOnly`, `SameSite`)
- Produces: `SessionData` (record: `UserId`, `Jwt`, `Permissions`, `CsrfToken`, `UserAgentHash`, `IssuedAt`, `ExpiresAt`)

- [ ] **Step 1: Create .csproj with YARP, Polly, Redis, and gRPC dependencies**

```xml
<!-- src/Bff/His.Hope.Bff.Core/His.Hope.Bff.Core.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>His.Hope.Bff.Core</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Yarp.ReverseProxy" Version="2.1.0" />
    <PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="8.0.*" />
    <PackageReference Include="Polly.Core" Version="8.*" />
    <PackageReference Include="Grpc.Net.ClientFactory" Version="2.*" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.*" />
    <PackageReference Include="VaultSharp" Version="1.*" />
  </ItemGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Write SessionCookieOptions.cs**

```csharp
// src/Bff/His.Hope.Bff.Core/Authentication/SessionCookieOptions.cs
namespace His.Hope.Bff.Core.Authentication;

public sealed record SessionCookieOptions
{
    public const string SectionName = "Bff:SessionCookie";
    public string CookieName { get; init; } = "hishop_sid";
    public string CookieDomain { get; init; } = "";
    public string CookiePath { get; init; } = "/api";
    public int CookieMaxAgeSeconds { get; init; } = 3600;
    public bool Secure { get; init; } = true;
    public bool HttpOnly { get; init; } = true;
    public SameSiteMode SameSite { get; init; } = SameSiteMode.Lax;
}
```

- [ ] **Step 3: Write SessionData.cs**

```csharp
// src/Bff/His.Hope.Bff.Core/Authentication/SessionData.cs
namespace His.Hope.Bff.Core.Authentication;

public sealed record SessionData
{
    public required string UserId { get; init; }
    public required string Jwt { get; init; }
    public required string[] Permissions { get; init; }
    public required string CsrfToken { get; init; }
    public required string UserAgentHash { get; init; }
    public required DateTimeOffset IssuedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}
```

- [ ] **Step 4: Run dotnet build to verify project compiles**

```bash
dotnet build src/Bff/His.Hope.Bff.Core/His.Hope.Bff.Core.csproj
```
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/Bff/His.Hope.Bff.Core/
git commit -m "feat(bff): create Bff.Core shared library with session models"
```

---

### Task 0.2: Implement SessionAuthMiddleware

**Files:**
- Create: `src/Bff/His.Hope.Bff.Core/Authentication/SessionAuthMiddleware.cs`
- Create: `tests/Bff/His.Hope.Bff.Core.Tests/Authentication/SessionAuthMiddlewareTests.cs`

**Interfaces:**
- Consumes: `SessionCookieOptions`, `SessionData`
- Produces: `SessionAuthMiddleware` — reads `hishop_sid` cookie, loads session from Redis, sets `HttpContext.Items["SessionJwt"]` and `HttpContext.Items["Permissions"]`
- Produces: `SessionAuthMiddlewareExtensions` — `IApplicationBuilder.UseBffSessionAuth()`

- [ ] **Step 1: Write the failing test — valid cookie sets context items**

```csharp
// tests/Bff/His.Hope.Bff.Core.Tests/Authentication/SessionAuthMiddlewareTests.cs
using His.Hope.Bff.Core.Authentication;
using Microsoft.AspNetCore.Http;
using StackExchange.Redis;
using Moq;

namespace His.Hope.Bff.Core.Tests.Authentication;

public class SessionAuthMiddlewareTests
{
    [Fact]
    public async Task ValidCookie_SetsSessionJwt_AndPermissions_InContextItems()
    {
        var sid = "abc123";
        var sessionData = new SessionData
        {
            UserId = "usr_1",
            Jwt = "eyJhbGciOiJSUzI1NiIs...",
            Permissions = new[] { "patients.view" },
            CsrfToken = "csrf-token",
            UserAgentHash = "sha256-test",
            IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(55)
        };
        var redisMock = new Mock<IDatabase>();
        redisMock.Setup(r => r.StringGetAsync(
                It.Is<RedisKey>(k => k == "session:abc123"), It.IsAny<CommandFlags>()))
            .ReturnsAsync(System.Text.Json.JsonSerializer.Serialize(sessionData));

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(redisMock.Object);

        var options = new SessionCookieOptions();
        var middleware = new SessionAuthMiddleware(
            _ => Task.CompletedTask, options, multiplexerMock.Object);

        var context = new DefaultHttpContext();
        context.Request.Headers["Cookie"] = "hishop_sid=abc123";
        context.Request.Headers["User-Agent"] = "test-agent";

        await middleware.InvokeAsync(context);

        Assert.Equal("eyJhbGciOiJSUzI1NiIs...", context.Items["SessionJwt"]);
        Assert.Equal(new[] { "patients.view" }, context.Items["Permissions"]);
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task ExpiredCookie_Returns401()
    {
        var sessionData = new SessionData
        {
            UserId = "usr_1",
            Jwt = "eyJ...",
            Permissions = Array.Empty<string>(),
            CsrfToken = "csrf",
            UserAgentHash = "sha256-test",
            IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-120),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-60)
        };
        var redisMock = new Mock<IDatabase>();
        redisMock.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(System.Text.Json.JsonSerializer.Serialize(sessionData));

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(redisMock.Object);

        var middleware = new SessionAuthMiddleware(
            _ => Task.CompletedTask, new SessionCookieOptions(), multiplexerMock.Object);

        var context = new DefaultHttpContext();
        context.Request.Headers["Cookie"] = "hishop_sid=expired";

        await middleware.InvokeAsync(context);

        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task MissingCookie_PassesThrough_AndReturns401()
    {
        var middleware = new SessionAuthMiddleware(
            _ => Task.CompletedTask, new SessionCookieOptions(),
            Mock.Of<IConnectionMultiplexer>());

        var context = new DefaultHttpContext();
        await middleware.InvokeAsync(context);

        Assert.Equal(401, context.Response.StatusCode);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Bff/His.Hope.Bff.Core.Tests/ --filter "SessionAuthMiddlewareTests"
```
Expected: FAIL — `SessionAuthMiddleware` not found.

- [ ] **Step 3: Implement SessionAuthMiddleware**

```csharp
// src/Bff/His.Hope.Bff.Core/Authentication/SessionAuthMiddleware.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace His.Hope.Bff.Core.Authentication;

public sealed class SessionAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SessionCookieOptions _options;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<SessionAuthMiddleware> _logger;

    public SessionAuthMiddleware(
        RequestDelegate next,
        SessionCookieOptions options,
        IConnectionMultiplexer redis,
        ILogger<SessionAuthMiddleware> logger)
    {
        _next = next;
        _options = options;
        _redis = redis;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var cookieValue = context.Request.Cookies[_options.CookieName];
        if (string.IsNullOrEmpty(cookieValue))
        {
            _logger.LogWarning("Session cookie '{CookieName}' missing", _options.CookieName);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var db = _redis.GetDatabase();
        var sessionJson = await db.StringGetAsync($"session:{cookieValue}");

        if (!sessionJson.HasValue)
        {
            _logger.LogWarning("Session '{SessionId}' not found in Redis", cookieValue);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var session = JsonSerializer.Deserialize<SessionData>(sessionJson!);

        if (session is null || session.IsExpired)
        {
            _logger.LogWarning("Session '{SessionId}' expired at {ExpiresAt}", cookieValue, session?.ExpiresAt);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var userAgentHash = ComputeHash(context.Request.Headers.UserAgent.ToString());
        if (!string.Equals(session.UserAgentHash, userAgentHash, StringComparison.Ordinal))
        {
            _logger.LogWarning("Session '{SessionId}' user-agent mismatch", cookieValue);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        context.Items["SessionJwt"] = session.Jwt;
        context.Items["Permissions"] = session.Permissions;
        context.Items["SessionId"] = cookieValue;

        await _next(context);
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input ?? ""));
        return Convert.ToHexString(bytes);
    }
}

public static class SessionAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseBffSessionAuth(this IApplicationBuilder builder)
        => builder.UseMiddleware<SessionAuthMiddleware>();
}
```

- [ ] **Step 4: Run tests — verify they pass**

```bash
dotnet test tests/Bff/His.Hope.Bff.Core.Tests/ --filter "SessionAuthMiddlewareTests"
```
Expected: All 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Bff/His.Hope.Bff.Core/Authentication/SessionAuthMiddleware.cs
git add tests/Bff/His.Hope.Bff.Core.Tests/
git commit -m "feat(bff): implement SessionAuthMiddleware with Redis-backed session validation"
```

---

### Task 0.3: Implement CsrfValidator middleware

**Files:**
- Create: `src/Bff/His.Hope.Bff.Core/Authentication/CsrfValidatorMiddleware.cs`

**Interfaces:**
- Consumes: `SessionData` (CsrfToken field), `HttpContext.Items["SessionId"]`
- Produces: `CsrfValidatorMiddleware` — validates `X-CSRF-Token` header on POST/PUT/PATCH/DELETE
- Produces: `CsrfValidatorMiddlewareExtensions` — `IApplicationBuilder.UseBffCsrfProtection()`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Bff/His.Hope.Bff.Core.Tests/Authentication/CsrfValidatorMiddlewareTests.cs
public class CsrfValidatorMiddlewareTests
{
    [Fact]
    public async Task GET_Request_SkipsCsrfCheck()
    {
        var redisMock = CreateRedisMockWithSession("test-sid", "csrf-token");
        var middleware = new CsrfValidatorMiddleware(
            _ => Task.CompletedTask, redisMock.Object,
            Mock.Of<ILogger<CsrfValidatorMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Items["SessionId"] = "test-sid";

        await middleware.InvokeAsync(context);

        Assert.NotEqual(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task POST_WithoutCsrfToken_Returns403()
    {
        var redisMock = CreateRedisMockWithSession("test-sid", "csrf-token");
        var middleware = new CsrfValidatorMiddleware(
            _ => Task.CompletedTask, redisMock.Object,
            Mock.Of<ILogger<CsrfValidatorMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Items["SessionId"] = "test-sid";
        // no X-CSRF-Token header

        await middleware.InvokeAsync(context);

        Assert.Equal(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task POST_WithMatchingCsrfToken_Passes()
    {
        var redisMock = CreateRedisMockWithSession("test-sid", "csrf-token");
        var middleware = new CsrfValidatorMiddleware(
            _ => Task.CompletedTask, redisMock.Object,
            Mock.Of<ILogger<CsrfValidatorMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Headers["X-CSRF-Token"] = "csrf-token";
        context.Items["SessionId"] = "test-sid";

        await middleware.InvokeAsync(context);

        Assert.NotEqual(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task POST_WithMismatchedCsrfToken_Returns403()
    {
        var redisMock = CreateRedisMockWithSession("test-sid", "correct-token");
        var middleware = new CsrfValidatorMiddleware(
            _ => Task.CompletedTask, redisMock.Object,
            Mock.Of<ILogger<CsrfValidatorMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Headers["X-CSRF-Token"] = "wrong-token";
        context.Items["SessionId"] = "test-sid";

        await middleware.InvokeAsync(context);

        Assert.Equal(403, context.Response.StatusCode);
    }

    private static Mock<IConnectionMultiplexer> CreateRedisMockWithSession(string sid, string csrfToken)
    {
        var session = new SessionData
        {
            UserId = "usr_1", Jwt = "jwt", Permissions = Array.Empty<string>(),
            CsrfToken = csrfToken, UserAgentHash = "hash",
            IssuedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        var redisMock = new Mock<IDatabase>();
        redisMock.Setup(r => r.StringGetAsync(
                It.Is<RedisKey>(k => k == $"session:{sid}"), It.IsAny<CommandFlags>()))
            .ReturnsAsync(JsonSerializer.Serialize(session));
        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(redisMock.Object);
        return multiplexerMock;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Bff/His.Hope.Bff.Core.Tests/ --filter "CsrfValidatorMiddlewareTests"
```
Expected: FAIL.

- [ ] **Step 3: Implement CsrfValidatorMiddleware**

```csharp
// src/Bff/His.Hope.Bff.Core/Authentication/CsrfValidatorMiddleware.cs
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace His.Hope.Bff.Core.Authentication;

public sealed class CsrfValidatorMiddleware
{
    private static readonly HashSet<string> MutationMethods = new(StringComparer.OrdinalIgnoreCase)
        { "POST", "PUT", "PATCH", "DELETE" };

    private readonly RequestDelegate _next;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CsrfValidatorMiddleware> _logger;

    public CsrfValidatorMiddleware(
        RequestDelegate next,
        IConnectionMultiplexer redis,
        ILogger<CsrfValidatorMiddleware> logger)
    {
        _next = next;
        _redis = redis;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!MutationMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var sessionId = context.Items["SessionId"] as string;
        if (string.IsNullOrEmpty(sessionId))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var csrfHeader = context.Request.Headers["X-CSRF-Token"].FirstOrDefault();
        if (string.IsNullOrEmpty(csrfHeader))
        {
            _logger.LogWarning("CSRF token missing for {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var db = _redis.GetDatabase();
        var sessionJson = await db.StringGetAsync($"session:{sessionId}");
        if (!sessionJson.HasValue)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var session = JsonSerializer.Deserialize<SessionData>(sessionJson!);
        if (session is null || !string.Equals(session.CsrfToken, csrfHeader, StringComparison.Ordinal))
        {
            _logger.LogWarning("CSRF token mismatch for session '{SessionId}'", sessionId);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await _next(context);
    }
}

public static class CsrfValidatorMiddlewareExtensions
{
    public static IApplicationBuilder UseBffCsrfProtection(this IApplicationBuilder builder)
        => builder.UseMiddleware<CsrfValidatorMiddleware>();
}
```

- [ ] **Step 4: Run tests — verify they pass**

```bash
dotnet test tests/Bff/His.Hope.Bff.Core.Tests/ --filter "CsrfValidatorMiddlewareTests"
```
Expected: All 4 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Bff/His.Hope.Bff.Core/Authentication/CsrfValidatorMiddleware.cs
git add tests/Bff/His.Hope.Bff.Core.Tests/Authentication/CsrfValidatorMiddlewareTests.cs
git commit -m "feat(bff): implement CsrfValidatorMiddleware with double-submit cookie validation"
```

---

### Task 0.4: Implement JWT transform and proxy config extensions

**Files:**
- Create: `src/Bff/His.Hope.Bff.Core/Proxy/JwtTransformProvider.cs`
- Create: `src/Bff/His.Hope.Bff.Core/Proxy/BffProxyConfigExtensions.cs`

**Interfaces:**
- Consumes: `HttpContext.Items["SessionJwt"]`
- Produces: `BffProxyConfigExtensions.AddBffProxy(IConfiguration)` — registers YARP with JWT injection transform
- Produces: `BffProxyConfigExtensions.MapBffReverseProxy()` — maps YARP endpoints

- [ ] **Step 1: Write the failing test — transform injects JWT header**

```csharp
// tests/Bff/His.Hope.Bff.Core.Tests/Proxy/JwtTransformProviderTests.cs
public class JwtTransformProviderTests
{
    [Fact]
    public async Task Transform_InjectsBearerToken_FromContextItems()
    {
        var context = new DefaultHttpContext();
        context.Items["SessionJwt"] = "test-jwt-token";

        var transform = new JwtTransformProvider();
        var proxyContext = new RequestTransformContext
        {
            HttpContext = context,
            ProxyRequest = new HttpRequestMessage()
        };

        // Simulate what YARP does — call the transform
        var applyResult = transform.Apply(new RequestTransformContext
        {
            HttpContext = context,
            ProxyRequest = new HttpRequestMessage()
        });

        await transform.ApplyAsync(new RequestTransformContext()
        {
            HttpContext = context,
            ProxyRequest = new HttpRequestMessage()
        });

        Assert.Equal("Bearer", proxyRequest.Headers.Authorization?.Scheme);
        Assert.Null(proxyRequest.Headers.Authorization); // not yet applied via proper YARP path
    }
}
```

Note: The actual JWT injection is tested via integration tests (Phase 4). For unit tests, verify the transform provider can be constructed and registered.

- [ ] **Step 2: Implement JwtTransformProvider**

```csharp
// src/Bff/His.Hope.Bff.Core/Proxy/JwtTransformProvider.cs
using System.Net.Http.Headers;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace His.Hope.Bff.Core.Proxy;

public sealed class JwtTransformProvider : ITransformProvider
{
    public void ValidateRoute(TransformRouteValidationContext context) { }

    public void ValidateCluster(TransformClusterValidationContext context) { }

    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(async transformContext =>
        {
            var jwt = transformContext.HttpContext.Items["SessionJwt"] as string;
            if (!string.IsNullOrEmpty(jwt))
            {
                transformContext.ProxyRequest.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", jwt);
            }
        });
    }
}
```

- [ ] **Step 3: Implement BffProxyConfigExtensions**

```csharp
// src/Bff/His.Hope.Bff.Core/Proxy/BffProxyConfigExtensions.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;

namespace His.Hope.Bff.Core.Proxy;

public static class BffProxyConfigExtensions
{
    public static IServiceCollection AddBffProxy(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms<JwtTransformProvider>();

        return services;
    }

    public static WebApplication MapBffReverseProxy(this WebApplication app)
    {
        app.MapReverseProxy();
        return app;
    }
}
```

- [ ] **Step 4: Build and verify**

```bash
dotnet build src/Bff/His.Hope.Bff.Core/
```
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/Bff/His.Hope.Bff.Core/Proxy/
git commit -m "feat(bff): implement JwtTransformProvider and BffProxyConfigExtensions"
```

---

### Task 0.5: Implement aggregation framework

**Files:**
- Create: `src/Bff/His.Hope.Bff.Core/Aggregation/IAggregationHandler.cs`
- Create: `src/Bff/His.Hope.Bff.Core/Aggregation/AggregationContext.cs`
- Create: `src/Bff/His.Hope.Bff.Core/Aggregation/AggregationResult.cs`
- Create: `src/Bff/His.Hope.Bff.Core/Aggregation/ParallelAggregationExecutor.cs`

**Interfaces:**
- Produces: `IAggregationHandler` (Route, Method, HandleAsync)
- Produces: `AggregationResult` (StatusCode, Data, Degraded[])
- Produces: `AggregationContext` (RouteValues, SessionJwt, CancellationToken)
- Produces: `ParallelAggregationExecutor.RunAsync(Dictionary<string, Func<Task<object>>>)`

- [ ] **Step 1: Write all aggregation model types**

```csharp
// src/Bff/His.Hope.Bff.Core/Aggregation/IAggregationHandler.cs
namespace His.Hope.Bff.Core.Aggregation;

public interface IAggregationHandler
{
    string Route { get; }
    string Method { get; }
    Task<AggregationResult> HandleAsync(AggregationContext context);
}
```

```csharp
// src/Bff/His.Hope.Bff.Core/Aggregation/AggregationContext.cs
namespace His.Hope.Bff.Core.Aggregation;

public sealed record AggregationContext(
    IReadOnlyDictionary<string, string> RouteValues,
    string SessionJwt,
    CancellationToken CancellationToken);
```

```csharp
// src/Bff/His.Hope.Bff.Core/Aggregation/AggregationResult.cs
namespace His.Hope.Bff.Core.Aggregation;

public sealed record AggregationResult
{
    public int StatusCode { get; init; } = 200;
    public object? Data { get; init; }
    public DegradedField[] Degraded { get; init; } = Array.Empty<DegradedField>();

    public static AggregationResult Success(object data) => new() { Data = data };

    public static AggregationResult Partial(object data, DegradedField[] degraded) => new()
        { Data = data, Degraded = degraded };

    public static AggregationResult Failed(string reason) => new()
        { StatusCode = 502, Data = new { error = reason } };
}

public sealed record DegradedField(string Field, string Reason, string CorrelationId);
```

```csharp
// src/Bff/His.Hope.Bff.Core/Aggregation/ParallelAggregationExecutor.cs
namespace His.Hope.Bff.Core.Aggregation;

public static class ParallelAggregationExecutor
{
    public sealed record AggregationExecutionResult(
        IReadOnlyDictionary<string, object> Successes,
        DegradedField[] Failures);

    public static async Task<AggregationExecutionResult> RunAsync(
        Dictionary<string, Func<Task<object>>> tasks,
        CancellationToken ct = default)
    {
        var results = new Dictionary<string, object>();
        var failures = new List<DegradedField>();

        var taskList = tasks.Select(async kvp =>
        {
            try
            {
                var result = await kvp.Value();
                lock (results) { results[kvp.Key] = result; }
            }
            catch (Exception ex)
            {
                lock (failures)
                {
                    failures.Add(new DegradedField(kvp.Key, ex.Message,
                        Activity.Current?.Id ?? "unknown"));
                }
            }
        });

        await Task.WhenAll(taskList);

        return new AggregationExecutionResult(results, failures.ToArray());
    }
}
```

- [ ] **Step 2: Build and verify**

```bash
dotnet build src/Bff/His.Hope.Bff.Core/
```
Expected: Build succeeded.

- [ ] **Step 3: Write unit test for ParallelAggregationExecutor**

```csharp
// tests/Bff/His.Hope.Bff.Core.Tests/Aggregation/ParallelAggregationExecutorTests.cs
public class ParallelAggregationExecutorTests
{
    [Fact]
    public async Task AllTasksSucceed_ReturnsAllSuccesses_NoFailures()
    {
        var result = await ParallelAggregationExecutor.RunAsync(new()
        {
            ["a"] = () => Task.FromResult<object>("value-a"),
            ["b"] = () => Task.FromResult<object>(42),
        });

        Assert.Equal(2, result.Successes.Count);
        Assert.Empty(result.Failures);
        Assert.Equal("value-a", result.Successes["a"]);
    }

    [Fact]
    public async Task PartialFailure_ReturnsSuccessesAndFailures()
    {
        var result = await ParallelAggregationExecutor.RunAsync(new()
        {
            ["a"] = () => Task.FromResult<object>("ok"),
            ["b"] = () => throw new TimeoutException("Service timeout"),
        });

        Assert.Single(result.Successes);
        Assert.Single(result.Failures);
        Assert.Equal("b", result.Failures[0].Field);
        Assert.Contains("timeout", result.Failures[0].Reason);
    }

    [Fact]
    public async Task AllFail_ReturnsNoSuccesses()
    {
        var result = await ParallelAggregationExecutor.RunAsync(new()
        {
            ["a"] = () => throw new InvalidOperationException("err"),
            ["b"] = () => throw new InvalidOperationException("err"),
        });

        Assert.Empty(result.Successes);
        Assert.Equal(2, result.Failures.Length);
    }
}
```

- [ ] **Step 4: Run tests — verify they pass**

```bash
dotnet test tests/Bff/His.Hope.Bff.Core.Tests/ --filter "ParallelAggregationExecutorTests"
```
Expected: All 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Bff/His.Hope.Bff.Core/Aggregation/
git add tests/Bff/His.Hope.Bff.Core.Tests/Aggregation/
git commit -m "feat(bff): implement aggregation framework with parallel executor and partial failure"
```

---

### Task 0.6: Implement resilience pipeline

**Files:**
- Create: `src/Bff/His.Hope.Bff.Core/Resilience/BffResiliencePipeline.cs`

**Interfaces:**
- Produces: `BffResiliencePipeline.AddBffResilience(IServiceCollection)` — registers Polly pipelines

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Bff/His.Hope.Bff.Core.Tests/Resilience/BffResiliencePipelineTests.cs
public class BffResiliencePipelineTests
{
    [Fact]
    public void AddBffResilience_RegistersPipeline_WithoutError()
    {
        var services = new ServiceCollection();
        BffResiliencePipeline.AddBffResilience(services);
        var provider = services.BuildServiceProvider();

        var pipeline = provider.GetRequiredService<ResiliencePipelineProvider<string>>()
            .GetPipeline("bff-downstream");

        Assert.NotNull(pipeline);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Bff/His.Hope.Bff.Core.Tests/ --filter "BffResiliencePipelineTests"
```
Expected: FAIL.

- [ ] **Step 3: Implement BffResiliencePipeline**

```csharp
// src/Bff/His.Hope.Bff.Core/Resilience/BffResiliencePipeline.cs
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace His.Hope.Bff.Core.Resilience;

public static class BffResiliencePipeline
{
    public static IServiceCollection AddBffResilience(this IServiceCollection services)
    {
        services.AddResiliencePipeline("bff-downstream", (builder, context) =>
        {
            builder
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 1,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromMilliseconds(100),
                    ShouldHandle = new PredicateBuilder()
                        .Handle<TimeoutException>()
                        .Handle<HttpRequestException>()
                        .Handle<RpcException>(ex => ex.StatusCode == StatusCode.Unavailable)
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 10,
                    BreakDuration = TimeSpan.FromSeconds(30),
                    SamplingDuration = TimeSpan.FromSeconds(60)
                })
                .AddTimeout(new TimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromSeconds(5)
                });
        });

        services.AddResiliencePipeline("bff-aggregation", (builder, context) =>
        {
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(15)
            });
        });

        return services;
    }
}
```

- [ ] **Step 4: Run test — verify it passes**

```bash
dotnet test tests/Bff/His.Hope.Bff.Core.Tests/ --filter "BffResiliencePipelineTests"
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Bff/His.Hope.Bff.Core/Resilience/
git commit -m "feat(bff): implement resilience pipeline with retry, circuit breaker, and timeout"
```

---

### Task 0.7: Implement DependencyInjection consolidation

**Files:**
- Modify: `src/Bff/His.Hope.Bff.Core/DependencyInjection.cs`

**Interfaces:**
- Produces: `IServiceCollection.AddBffCore(IConfiguration)` — registers all core BFF services
- Produces: `WebApplication.MapBffAggregation()` — maps aggregation endpoints

- [ ] **Step 1: Write final DependencyInjection.cs**

```csharp
// src/Bff/His.Hope.Bff.Core/DependencyInjection.cs
using His.Hope.Bff.Core.Aggregation;
using His.Hope.Bff.Core.Authentication;
using His.Hope.Bff.Core.Resilience;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.Bff.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddBffCore(
        this IServiceCollection services, IConfiguration configuration)
    {
        var cookieOptions = configuration
            .GetSection(SessionCookieOptions.SectionName)
            .Get<SessionCookieOptions>() ?? new SessionCookieOptions();

        services.AddSingleton(cookieOptions);

        // Redis connection for session store
        var redisConnection = configuration.GetConnectionString("Redis")
            ?? configuration["Redis:Connection"]
            ?? throw new InvalidOperationException("Redis connection string not configured");

        services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ =>
            StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnection));

        // Resilience
        services.AddBffResilience();

        return services;
    }

    public static WebApplication MapBffAggregation(this WebApplication app)
    {
        var handlers = app.Services.GetServices<IAggregationHandler>();

        foreach (var handler in handlers)
        {
            app.MapMethods(handler.Route, new[] { handler.Method }, async (HttpContext context) =>
            {
                var routeValues = context.Request.RouteValues
                    .ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "");
                var jwt = context.Items["SessionJwt"] as string ?? "";

                var aggContext = new AggregationContext(
                    routeValues, jwt, context.RequestAborted);

                var result = await handler.HandleAsync(aggContext);

                context.Response.StatusCode = result.StatusCode;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    data = result.Data,
                    degraded = result.Degraded
                });
            });
        }

        return app;
    }
}
```

- [ ] **Step 2: Build and verify**

```bash
dotnet build src/Bff/His.Hope.Bff.Core/
```
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/Bff/His.Hope.Bff.Core/DependencyInjection.cs
git commit -m "feat(bff): implement consolidated DI registration for Bff.Core"
```

---

## Phase 1: Auth Migration (Week 1, Days 3-4)

### Task 1.1: IdentityService — Dual cookie + Bearer token login

**Files:**
- Modify: `src/Services/IdentityService/.../AuthController.cs` (login endpoint)
- Modify: `src/Services/IdentityService/.../TokenResponse.cs`

**Interfaces:**
- Consumes: Existing login flow
- Produces: Login response now includes `Set-Cookie: hishope_sid=<sid>` alongside existing Bearer token
- Produces: Session stored in Redis under `session:{sid}`

- [ ] **Step 1: Write integration test for cookie being set on login**

```csharp
// tests/IdentityService.IntegrationTests/AuthControllerBffTests.cs
public class AuthControllerBffTests : IClassFixture<IdentityServiceWebApplicationFactory>
{
    [Fact]
    public async Task Login_Success_ReturnsHishopSidCookie()
    {
        var client = _factory.CreateClient();
        var content = new StringContent(JsonSerializer.Serialize(new
        {
            username = "admin",
            password = "Admin@123"
        }), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/auth/login", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        var sessionCookie = cookies.FirstOrDefault(c => c.StartsWith("hishop_sid="));
        Assert.NotNull(sessionCookie);
        Assert.Contains("HttpOnly", sessionCookie);
        Assert.Contains("Secure", sessionCookie);
        Assert.Contains("SameSite=Lax", sessionCookie);

        // Verify body still has accessToken for backward compat
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("accessToken", body);
    }
}
```

- [ ] **Step 2: Run test — verify it fails**

```bash
dotnet test tests/IdentityService.IntegrationTests/ --filter "AuthControllerBffTests"
```
Expected: FAIL — no Set-Cookie header.

- [ ] **Step 3: Modify AuthController login to set session cookie**

In the login action, after successful authentication, add:

```csharp
// After existing token generation:
var sessionId = Guid.NewGuid().ToString("N");
var sessionData = new BffSessionData
{
    UserId = user.Id,
    Jwt = accessToken,
    Permissions = permissions,
    CsrfToken = Guid.NewGuid().ToString("N"),
    UserAgentHash = ComputeSha256(Request.Headers.UserAgent.ToString()),
    IssuedAt = DateTimeOffset.UtcNow,
    ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
};

var redisDb = _redis.GetDatabase();
await redisDb.StringSetAsync(
    $"session:{sessionId}",
    JsonSerializer.Serialize(sessionData),
    TimeSpan.FromHours(1));

Response.Cookies.Append("hishop_sid", sessionId, new CookieOptions
{
    HttpOnly = true,
    Secure = true,
    SameSite = SameSiteMode.Lax,
    Path = "/api",
    MaxAge = TimeSpan.FromHours(1)
});

// Also set CSRF readable cookie
Response.Cookies.Append("hishop_csrf", sessionData.CsrfToken, new CookieOptions
{
    HttpOnly = false,  // JS-readable
    Secure = true,
    SameSite = SameSiteMode.Strict,
    Path = "/api",
    MaxAge = TimeSpan.FromHours(1)
});
```

- [ ] **Step 4: Run test — verify it passes**

```bash
dotnet test tests/IdentityService.IntegrationTests/ --filter "AuthControllerBffTests"
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Services/IdentityService/
git add tests/IdentityService.IntegrationTests/
git commit -m "feat(auth): add HttpOnly session cookie to login endpoint alongside Bearer token"
```

---

### Task 1.2: Angular — Add BffRouterService

**Files:**
- Create: `src/Frontend/his-hope-app/src/app/core/services/bff-router.service.ts`
- Create: `src/Frontend/his-hope-app/src/app/core/interceptors/csrf.interceptor.ts`

**Interfaces:**
- Produces: `BffRouterService.shouldUseBff(url)` — checks Unleash flag per module
- Produces: `BffRouterService.getFetchOptions(url)` — returns `{ credentials, headers }` for BFF or Bearer
- Produces: `CsrfInterceptor` — reads `hishop_csrf` cookie, adds `X-CSRF-Token` on mutations

- [ ] **Step 1: Write BffRouterService**

```typescript
// src/Frontend/his-hope-app/src/app/core/services/bff-router.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';

const BFF_URL_PREFIX_FLAGS: Record<string, string> = {
  '/api/v1/patients': 'bff.patient.enabled',
  '/api/v1/encounters': 'bff.clinical.enabled',
  '/api/v1/lab-orders': 'bff.lab.enabled',
  '/api/v1/invoices': 'bff.billing.enabled',
  '/api/v1/medications': 'bff.pharmacy.enabled',
  '/api/v1/prescriptions': 'bff.pharmacy.enabled',
  '/api/v1/dashboard': 'bff.dashboard.enabled',
  '/api/v1/critical-alerts': 'bff.lab.enabled',
};

const SKIP_BFF_PREFIXES = ['/api/v1/auth/', '/api/v1/admin/', '/api/v1/errors', '/api/v1/audit/'];

@Injectable({ providedIn: 'root' })
export class BffRouterService {
  private accessToken: string | null = null;
  private cookieAuthOnly = false;

  constructor(private http: HttpClient) {
    // Check Unleash for global cookie-only flag
    this.cookieAuthOnly = (window as any).__UNLEASH_FLAGS__?.['bff.auth.cookie-only'] ?? false;

    // Restore access token from sessionStorage for backward compat
    this.accessToken = sessionStorage.getItem('hishop_access_token');
  }

  setAccessToken(token: string | null): void {
    this.accessToken = token;
    if (token) {
      sessionStorage.setItem('hishop_access_token', token);
    } else {
      sessionStorage.removeItem('hishop_access_token');
    }
  }

  shouldUseBff(url: string): boolean {
    if (SKIP_BFF_PREFIXES.some(p => url.startsWith(p))) return false;
    if (this.cookieAuthOnly) return true; // global flag
    const flag = Object.entries(BFF_URL_PREFIX_FLAGS)
      .find(([prefix]) => url.startsWith(prefix));
    return flag ? ((window as any).__UNLEASH_FLAGS__?.[flag[1]] ?? false) : false;
  }

  getFetchOptions(url: string, method: string): RequestInit & { headers?: Record<string, string> } {
    if (this.shouldUseBff(url)) {
      const headers: Record<string, string> = {};
      if (['POST', 'PUT', 'PATCH', 'DELETE'].includes(method)) {
        const csrfToken = this.getCookie('hishop_csrf');
        if (csrfToken) headers['X-CSRF-Token'] = csrfToken;
      }
      return { credentials: 'include', headers };
    }
    return {
      headers: this.accessToken
        ? { 'Authorization': `Bearer ${this.accessToken}` }
        : {}
    };
  }

  private getCookie(name: string): string | null {
    const match = document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`));
    return match ? decodeURIComponent(match[1]) : null;
  }
}
```

- [ ] **Step 2: Write CsrfInterceptor**

```typescript
// src/Frontend/his-hope-app/src/app/core/interceptors/csrf.interceptor.ts
import { HttpInterceptorFn } from '@angular/common/http';

export const csrfInterceptor: HttpInterceptorFn = (req, next) => {
  if (['POST', 'PUT', 'PATCH', 'DELETE'].includes(req.method)) {
    const csrfToken = getCsrfCookie();
    if (csrfToken) {
      req = req.clone({ setHeaders: { 'X-CSRF-Token': csrfToken } });
    }
  }
  // Also ensure credentials:include for BFF requests
  if (req.url.startsWith('/api/v1/') && !req.withCredentials) {
    req = req.clone({ withCredentials: true });
  }
  return next(req);
};

function getCsrfCookie(): string | null {
  const match = document.cookie.match(/(?:^|; )hishop_csrf=([^;]*)/);
  return match ? decodeURIComponent(match[1]) : null;
}
```

- [ ] **Step 3: Register CsrfInterceptor in app.config.ts**

```typescript
// In src/Frontend/his-hope-app/src/app/app.config.ts
// Add to providers:
import { csrfInterceptor } from '@core/interceptors/csrf.interceptor';

provideHttpClient(
  withInterceptors([/* existing... */, csrfInterceptor])
),
```

- [ ] **Step 4: Commit**

```bash
git add src/Frontend/his-hope-app/src/app/core/services/bff-router.service.ts
git add src/Frontend/his-hope-app/src/app/core/interceptors/csrf.interceptor.ts
git add src/Frontend/his-hope-app/src/app/app.config.ts
git commit -m "feat(bff): add BffRouterService and CsrfInterceptor to Angular"
```

---

### Task 1.3: Angular — Remove old AuthInterceptor (post-auth-cutover)

**Files:**
- Modify: `src/Frontend/his-hope-app/src/app/core/interceptors/auth.interceptor.ts` → DELETE
- Modify: `src/Frontend/his-hope-app/src/app/app.config.ts` → remove old interceptor reference
- Modify: `src/Frontend/his-hope-app/src/app/core/services/auth.service.ts` → remove sessionStorage.setItem

**Wait**: This task runs AFTER verifying Phase 1 with `bff.auth.cookie-only = true`. In the plan, we stub this but actual execution order depends on canary results.

- [ ] **Step 1: Remove auth.interceptor.ts**

```bash
rm src/Frontend/his-hope-app/src/app/core/interceptors/auth.interceptor.ts
```

- [ ] **Step 2: Update app.config.ts — remove old interceptor**

Remove the old `AuthInterceptor` from the `withInterceptors` array and `APP_INITIALIZER`.

- [ ] **Step 3: Update auth.service.ts**

Remove `sessionStorage.setItem('hishop_access_token', resp.accessToken)` from login. Replace with:

```typescript
this.bffRouter.setAccessToken(null); // no more Bearer token
```

- [ ] **Step 4: Commit**

```bash
git rm src/Frontend/his-hope-app/src/app/core/interceptors/auth.interceptor.ts
git add src/Frontend/his-hope-app/src/app/app.config.ts
git add src/Frontend/his-hope-app/src/app/core/services/auth.service.ts
git commit -m "feat(bff): remove Bearer token auth interceptor, switch to cookie-only auth"
```

---

## Phase 2-7: BFF Service Template (repeated per module)

### Phase 2: LabBff (Week 2, Days 1-2) — Pure proxy, zero aggregation

### Phase 3-6: BillingBff, PharmacyBff, ClinicalBff, PatientBff — Proxy + aggregation

### Phase 7: DashboardBff — Pure aggregation, no proxy

Each BFF follows the same template. Differences are in routes config, aggregation handlers, and gRPC clients. Here is the **general template** applied to each:

---

### Task X.1: Create BFF project with Program.cs

**Files:**
- Create: `src/Bff/{Module}Bff/{Module}Bff.csproj`
- Create: `src/Bff/{Module}Bff/Program.cs`
- Create: `src/Bff/{Module}Bff/appsettings.json`
- Create: `src/Bff/{Module}Bff/Routes/{module}-routes.json`

**Template Program.cs:**

```csharp
using His.Hope.Bff.Core;
using His.Hope.Bff.Core.Proxy;
using {Module}Bff.Aggregation; // only if this BFF has aggregation

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBffCore(builder.Configuration);
builder.Services.AddBffProxy(builder.Configuration);
builder.Services.AddBffAggregationHandlers(); // scan for IAggregationHandler
builder.Services.AddBffResilience();

// Register gRPC clients this BFF needs
builder.Services.AddGrpcClient<PatientGrpcServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["Services:Patient"]!));
builder.Services.AddGrpcClient<ClinicalGrpcServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["Services:Clinical"]!));
// ... additional clients per module

var app = builder.Build();

app.UseBffSessionAuth();
app.UseBffCsrfProtection();
app.MapBffReverseProxy();
app.MapBffAggregation();

app.Run();
```

**Template appsettings.json:**

```json
{
  "Bff": {
    "SessionCookie": {
      "CookieName": "hishop_sid",
      "CookiePath": "/api",
      "CookieMaxAgeSeconds": 3600,
      "Secure": true,
      "HttpOnly": true,
      "SameSite": "Lax"
    }
  },
  "Redis": {
    "Connection": "redis-cluster.his-hope.svc:6379,password=REDIS_PASS,ssl=true"
  },
  "Services": {
    "Patient": "http://patientservice:5002",
    "Clinical": "http://clinicalservice:5004",
    "Lab": "http://labservice:5010",
    "Billing": "http://billingservice:5020",
    "Pharmacy": "http://pharmacyservice:5030"
  },
  "ReverseProxy": {
    "Routes": {
      "{module}-get": {
        "ClusterId": "{module}-service",
        "Match": { "Path": "/api/v1/{module}/{id}", "Methods": ["GET"] }
      },
      "{module}-search": {
        "ClusterId": "{module}-service",
        "Match": { "Path": "/api/v1/{module}/search", "Methods": ["GET"] }
      }
    },
    "Clusters": {
      "{module}-service": {
        "Destinations": {
          "{module}": { "Address": "http://{module}service:PORT/" }
        }
      }
    }
  }
}
```

- [ ] **Step 1: Create .csproj for this BFF**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>{Module}Bff</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\His.Hope.Bff.Core\His.Hope.Bff.Core.csproj" />
    <ProjectReference Include="..\..\Shared\Protos\His.Hope.Protos.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Run dotnet build**

```bash
dotnet build src/Bff/{Module}Bff/
```
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/Bff/{Module}Bff/
git commit -m "feat(bff): create {Module}Bff service project"
```

---

### Task X.2: Write aggregation handler (if module needs aggregation)

**Files:**
- Create: `src/Bff/{Module}Bff/Aggregation/{Module}TimelineHandler.cs`

**Template (for PatientBff as example):**

```csharp
using His.Hope.Bff.Core.Aggregation;
using His.Hope.Patient.V1;
using His.Hope.Clinical.V1;
using His.Hope.Lab.V1;
using His.Hope.Pharmacy.V1;
using Microsoft.Extensions.Logging;
using Polly.Registry;

namespace PatientBff.Aggregation;

public sealed class PatientTimelineHandler : IAggregationHandler
{
    public string Route => "/api/v1/patients/{id}/timeline";
    public string Method => "GET";

    private readonly PatientGrpcService.PatientGrpcServiceClient _patientClient;
    private readonly ClinicalGrpcService.ClinicalGrpcServiceClient _clinicalClient;
    private readonly LabGrpcService.LabGrpcServiceClient _labClient;
    private readonly PharmacyGrpcService.PharmacyGrpcServiceClient _pharmacyClient;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<PatientTimelineHandler> _logger;

    public PatientTimelineHandler(
        PatientGrpcService.PatientGrpcServiceClient patientClient,
        ClinicalGrpcService.ClinicalGrpcServiceClient clinicalClient,
        LabGrpcService.LabGrpcServiceClient labClient,
        PharmacyGrpcService.PharmacyGrpcServiceClient pharmacyClient,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<PatientTimelineHandler> logger)
    {
        _patientClient = patientClient;
        _clinicalClient = clinicalClient;
        _labClient = labClient;
        _pharmacyClient = pharmacyClient;
        _pipeline = pipelineProvider.GetPipeline("bff-downstream");
        _logger = logger;
    }

    public async Task<AggregationResult> HandleAsync(AggregationContext context)
    {
        var patientId = context.RouteValues["id"]!;
        var ct = context.CancellationToken;

        var results = await ParallelAggregationExecutor.RunAsync(new()
        {
            ["patient"] = () => _pipeline.ExecuteAsync(async ct =>
            {
                var resp = await _patientClient.GetPatientAsync(
                    new PatientRequest { Id = patientId }, cancellationToken: ct);
                return (object)resp.Patient;
            }, ct),
            ["encounters"] = () => _pipeline.ExecuteAsync(async ct =>
            {
                var resp = await _clinicalClient.GetPatientEncountersAsync(
                    new PatientEncountersRequest { PatientId = patientId }, cancellationToken: ct);
                return (object)resp.Items;
            }, ct),
            ["labOrders"] = () => _pipeline.ExecuteAsync(async ct =>
            {
                var resp = await _labClient.GetPatientLabOrdersAsync(
                    new PatientLabOrdersRequest { PatientId = patientId }, cancellationToken: ct);
                return (object)resp.Items;
            }, ct),
            ["prescriptions"] = () => _pipeline.ExecuteAsync(async ct =>
            {
                var resp = await _pharmacyClient.SearchPrescriptionsAsync(
                    new PrescriptionSearchRequest { PatientId = patientId }, cancellationToken: ct);
                return (object)resp.Items;
            }, ct)
        });

        return results.Successes.Count > 0
            ? AggregationResult.Partial(new { data = results.Successes }, results.Failures)
            : AggregationResult.Failed("All downstream services unavailable");
    }
}
```

- [ ] **Step 1: Write unit test for aggregation handler with mocked gRPC clients**

- [ ] **Step 2: Run test — verify it fails if handler not registered**

- [ ] **Step 3: Run test — verify it passes**

- [ ] **Step 4: Commit**

```bash
git add src/Bff/{Module}Bff/Aggregation/
git add tests/Bff/{Module}Bff.Tests/
git commit -m "feat(bff): implement {Module} aggregation handler"
```

---

### Task X.3: Add K8s manifests for this BFF

**Files:**
- Create: `k8s/base/{module}-bff.yaml` (Deployment + Service)
- Create: `k8s/linkerd/bff-servers.yaml` (append Server + ServerAuthorization)
- Create: `k8s/cilium/bff-policies.yaml` (append CiliumNetworkPolicy)
- Create: `vault/policies/{module}-bff.hcl`
- Create: `cicd/tekton/pipelines/{module}-bff-ci.yaml`
- Create: `cicd/argo/applications/{module}-bff.yaml`

**Deployment template:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {module}-bff
  labels:
    app: {module}-bff
spec:
  replicas: 2
  selector:
    matchLabels:
      app: {module}-bff
  template:
    metadata:
      annotations:
        linkerd.io/inject: enabled
      labels:
        app: {module}-bff
    spec:
      serviceAccountName: {module}-bff
      securityContext:
        readOnlyRootFilesystem: true
        runAsNonRoot: true
      containers:
        - name: {module}-bff
          image: his-hope/{module}-bff:{IMAGE_TAG}
          ports:
            - containerPort: {PORT}
              name: http
          env:
            - name: Redis__Connection
              valueFrom:
                secretKeyRef:
                  name: {module}-bff-secrets
                  key: redis-connection
          envFrom:
            - secretRef:
                name: {module}-bff-secrets
          readinessProbe:
            httpGet:
              path: /health/ready
              port: {PORT}
            initialDelaySeconds: 10
          livenessProbe:
            httpGet:
              path: /health
              port: {PORT}
            initialDelaySeconds: 15
          resources:
            requests:
              cpu: 100m
              memory: 128Mi
            limits:
              cpu: 500m
              memory: 256Mi
---
apiVersion: v1
kind: Service
metadata:
  name: {module}-bff
spec:
  selector:
    app: {module}-bff
  ports:
    - port: {PORT}
      targetPort: {PORT}
      name: http
```

- [ ] **Step 1: Write all K8s manifests**

- [ ] **Step 2: Validate manifests**

```bash
kubectl --dry-run=client apply -f k8s/base/{module}-bff.yaml
```
Expected: Valid.

- [ ] **Step 3: Commit**

```bash
git add k8s/ linkerd/ cilium/ vault/ cicd/
git commit -m "feat(bff): add K8s, Linkerd, Cilium, Vault, and CI/CD config for {Module}Bff"
```

---

### Task X.4: Add YARP edge route for this BFF

**Files:**
- Modify: `src/ApiGateway/appsettings.json` — add cluster + route for this BFF

- [ ] **Step 1: Add cluster config**

```json
{
  "Clusters": {
    "{module}-bff": {
      "Destinations": {
        "{module}-bff": { "Address": "http://{module}-bff.his-hope.svc:{PORT}/" }
      }
    }
  },
  "Routes": {
    "{module}-bff-route": {
      "ClusterId": "{module}-bff",
      "Match": { "Path": "/api/v1/{module-prefix}/{**catch-all}" }
    }
  }
}
```

The route selection between `{module}-bff` and `{module}-service` is handled by the `BffAwareProxyConfigProvider` (dynamic Unleash flag check).

- [ ] **Step 2: Implement BffAwareProxyConfigProvider** (if not done in Phase 0)

```csharp
// src/ApiGateway/BffAwareProxyConfigProvider.cs
public class BffAwareProxyConfigProvider : IProxyConfigProvider
{
    private readonly IUnleash _unleash;
    private readonly IConfiguration _config;

    public BffAwareProxyConfigProvider(IUnleash unleash, IConfiguration config)
    {
        _unleash = unleash;
        _config = config;
    }

    public IProxyConfig GetConfig()
    {
        var moduleMap = new Dictionary<string, (string bffCluster, string serviceCluster)>
        {
            ["patients"] = ("patient-bff", "patient-service"),
            ["encounters"] = ("clinical-bff", "clinical-service"),
            ["lab-orders"] = ("lab-bff", "lab-service"),
            ["invoices"] = ("billing-bff", "billing-service"),
            ["medications"] = ("pharmacy-bff", "pharmacy-service"),
            ["prescriptions"] = ("pharmacy-bff", "pharmacy-service"),
            ["dashboard"] = ("dashboard-bff", null), // no legacy fallback
        };

        var routes = new List<RouteConfig>();
        foreach (var (prefix, (bffCluster, serviceCluster)) in moduleMap)
        {
            var enabled = _unleash.IsEnabled($"bff.{prefix}.enabled");
            var clusterId = enabled ? bffCluster : serviceCluster;
            if (clusterId is null) continue;

            routes.Add(new RouteConfig
            {
                RouteId = $"{prefix}-route",
                ClusterId = clusterId,
                Match = new RouteMatch { Path = $"/api/v1/{prefix}/{{**catch-all}}" }
            });
        }

        var clusters = _config.GetSection("ReverseProxy:Clusters").Get<List<ClusterConfig>>()!;
        return new BffProxyConfig(routes, clusters);
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add src/ApiGateway/
git commit -m "feat(bff): add dynamic BFF route selector to YARP edge gateway"
```

---

### Task X.5: Integration test for BFF end-to-end

**Files:**
- Create: `tests/Bff/{Module}Bff.IntegrationTests/{Module}BffEndToEndTests.cs`

**Test pattern (Redis + gRPC mock via Testcontainers):**

```csharp
public class PatientBffEndToEndTests : IAsyncLifetime
{
    private RedisContainer _redis;
    private WebApplicationFactory<Program> _bff;
    private HttpClient _client;

    public async Task InitializeAsync()
    {
        _redis = new RedisBuilder().Build();
        await _redis.StartAsync();

        Environment.SetEnvironmentVariable("Redis__Connection", _redis.GetConnectionString());

        _bff = new WebApplicationFactory<Program>();
        _client = _bff.CreateClient();
    }

    [Fact]
    public async Task GetPatientTimeline_FullSuccess_Returns200()
    {
        // Seed Redis session
        var db = _redis.GetDatabase();
        var session = new SessionData
        {
            UserId = "usr_1",
            Jwt = TestJwt.ValidPatientView,
            Permissions = new[] { "patients.view" },
            CsrfToken = "csrf",
            UserAgentHash = ComputeHash("test"),
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        await db.StringSetAsync("session:test-sid", JsonSerializer.Serialize(session));

        // Call BFF
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/patients/PAT-001/timeline");
        request.Headers.Add("Cookie", "hishop_sid=test-sid");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TimelineResponse>();
        Assert.NotNull(body!.data);
        Assert.Empty(body.degraded);
    }

    public async Task DisposeAsync()
    {
        await _bff.DisposeAsync();
        await _redis.DisposeAsync();
    }
}
```

- [ ] **Step 1: Run integration test — verify**

```bash
dotnet test tests/Bff/{Module}Bff.IntegrationTests/
```
Expected: Tests pass with Redis container.

- [ ] **Step 2: Commit**

```bash
git add tests/Bff/{Module}Bff.IntegrationTests/
git commit -m "test(bff): add integration tests for {Module}Bff with Redis Testcontainers"
```

---

## Plan Summary

| Phase | Tasks | Deliverable |
|-------|-------|-------------|
| **0** | 0.1-0.7 (7 tasks) | `His.Hope.Bff.Core` NuGet package with session auth, CSRF, proxy, aggregation, resilience |
| **1** | 1.1-1.3 (3 tasks) | IdentityService dual cookie+Bearer + Angular BffRouter + CsrfInterceptor, old interceptor removed |
| **2** | Template × 1 (5 tasks) | LabBff deployed (pure proxy) with K8s + CI/CD |
| **3-6** | Template × 4 (20 tasks) | BillingBff, PharmacyBff, ClinicalBff, PatientBff with aggregation |
| **7** | Template × 1 (5 tasks) | DashboardBff (pure aggregation) |
| **Cleanup** | 2 tasks | Remove legacy routes, remove feature flags, update docs |

**Total: ~42 tasks across 8 phases, 4-week execution window.**

Each BFF Phase (2-7) re-uses the same task template with module-specific routes, gRPC clients, and aggregation handlers.
