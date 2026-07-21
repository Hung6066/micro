# Task 1: Create SystemDashboard.Bff Project Scaffold — Report

**Status:** DONE

## Summary

Created the `src/Bff/SystemDashboard.Bff/` project scaffold for the Aspire-like SystemDashboard BFF service.

## Files Created

| File | Description |
|------|-------------|
| `SystemDashboard.Bff.csproj` | .NET 8 Web project with SignalR, Polly, OpenTelemetry, JWT Bearer, and project references to `His.Hope.Infrastructure` and `His.Hope.Bff.Core` |
| `GlobalUsings.cs` | Common global usings (System, Microsoft.AspNetCore.Mvc, etc.) |
| `appsettings.json` | Config with Consul, Elasticsearch, Jaeger, Prometheus, Docker, Kubernetes, and Jwt settings |
| `appsettings.Development.json` | Development overrides with Debug-level logging |
| `Properties/launchSettings.json` | Launch profile on ports 5700 (HTTP) / 5701 (HTTPS) |
| `Program.cs` | Minimal API skeleton with JSON camelCase serialization, JWT auth, CORS (localhost:4201), SignalR, health check at `/health`, and OpenTelemetry tracing + metrics |

## Build Verification

```
dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj
```
→ **Build succeeded.** 0 warnings, 0 errors.

## Files Created (6 total)

- `src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj`
- `src/Bff/SystemDashboard.Bff/GlobalUsings.cs`
- `src/Bff/SystemDashboard.Bff/appsettings.json`
- `src/Bff/SystemDashboard.Bff/appsettings.Development.json`
- `src/Bff/SystemDashboard.Bff/Properties/launchSettings.json`
- `src/Bff/SystemDashboard.Bff/Program.cs`

## Notes

- No database dependencies (as specified — no controllers yet).
- OpenTelemetry required explicit `using OpenTelemetry; using OpenTelemetry.Metrics; using OpenTelemetry.Trace;` directives — these were added to Program.cs to fix initial build errors.
- Polly version (8.7.0) and OpenTelemetry versions (1.15.3 / 1.8.1) match those used by `His.Hope.Infrastructure`.
- SignalR is referenced via `Microsoft.AspNetCore.SignalR.Common` package (included in ASP.NET Core framework).
