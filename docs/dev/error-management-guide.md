# His.Hope Error Management Guide

> **Document:** DEV-ERR-001
> **Version:** 1.0
> **Audience:** Backend Developer, Frontend Developer, SRE, DevOps
> **Last updated:** 2026-07-17

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Correlation ID](#2-correlation-id)
3. [Exception Handling](#3-exception-handling)
4. [ProblemDetails Format](#4-problemdetails-format)
5. [Distributed Tracing](#5-distributed-tracing)
6. [MediatR Tracing](#6-mediatr-tracing)
7. [Frontend Error Handling](#7-frontend-error-handling)
8. [Alerting](#8-alerting)
9. [Grafana Dashboard](#9-grafana-dashboard)
10. [Adding a New Service](#10-adding-a-new-service)

---

## 1. Architecture Overview

The error management system is built on three layers:

```
Layer 1: CAPTURE
  Backend:   CorrelationIdMiddleware → GlobalExceptionMiddleware → ProblemDetails (RFC 7807)
  Frontend:  ErrorInterceptor → ErrorService → GlobalErrorHandler → ErrorBarComponent
  gRPC:      GrpcGlobalExceptionInterceptor → gRPC Status codes

Layer 2: TRACE
  MediatR:   TracingBehaviour → OpenTelemetry spans → Jaeger
  Logging:   Serilog (correlationId + traceId enriched) → Elasticsearch → Kibana
  Metrics:   OpenTelemetry → Prometheus → Grafana

Layer 3: ALERT
  Prometheus rules → AlertManager → Slack (#his-hope-alerts, #his-hope-critical) + Email (oncall)
  Grafana Error Tracking dashboard (7 panels)
```

### End-to-End Error Flow

```
┌──────────────┐     ┌──────────────────┐     ┌───────────────────┐     ┌────────────────┐
│  Angular      │     │  YARP Gateway    │     │  Backend Service  │     │  Database      │
│  Frontend     │     │                  │     │                   │     │                │
│               │     │                  │     │                   │     │                │
│  Request ─────┼────►│ X-Correlation-Id ┼────►│ CorrelationIdMdw ─┼────►│ Query         │
│  (may set     │     │ (propagate)      │     │       │           │     │                │
│  X-Correlation│     │                  │     │  TracingBehaviour │     │                │
│  -Id header   │     │                  │     │       │           │     │                │
│  or generate) │     │                  │     │  GlobalException  │     │                │
│               │     │                  │     │  Middleware       │     │                │
│               │     │                  │     │       │           │     │                │
│ ErrorIntercept│◄────┤ ProblemDetails   ◄─────┤ ProblemDetails   ◄─────┤                │
│       │       │     │ + X-Correlation-Id│    │ (RFC 7807)        │     │                │
│       ▼       │     │                  │     │       │           │     │                │
│  ErrorService │     │                  │     │  OpenTelemetry   │     │                │
│       │       │     │                  │     │  spans emitted   │     │                │
│       ▼       │     │                  │     │       │           │     │                │
│  GlobalError  │     │                  │     │  Log (Serilog)   │     │                │
│  Handler      │     │                  │     │  with traceId +  │     │                │
│       │       │     │                  │     │  correlationId   │     │                │
│       ▼       │     │                  │     │                   │     │                │
│  ErrorBar     │     │                  │     │                   │     │                │
│  Component    │     │                  │     │                   │     │                │
└───────┬───────┘     └──────────────────┘     └────────┬──────────┘     └────────────────┘
        │                                                │
        │                                          ┌─────▼──────┐
        │                                          │  Prometheus │
        │                                          │  metrics    │
        │                                          └─────┬──────┘
        │                                                │
        │                                          ┌─────▼──────┐
        │                                          │ AlertManager│
        │                                          └─────┬──────┘
        │                                                │
        │                                    ┌───────────┴────────────┐
        │                                    │                        │
        │                              ┌─────▼──────┐          ┌─────▼──────┐
        │                              │ Slack      │          │ Email      │
        │                              │ (#his-hope │          │ (oncall@)  │
        │                              │  -alerts)  │          │            │
        │                              └────────────┘          └────────────┘
        │
   ┌────▼─────┐     ┌──────────┐     ┌───────────┐
   │  Jaeger  │     │ Kibana   │     │  Grafana  │
   │  Tracing │     │ Logs     │     │  Error    │
   │  UI      │     │ (ELK)    │     │  Tracking │
   └──────────┘     └──────────┘     │  Dashboard│
                                     └───────────┘
```

### Component Locations

| Component | Path |
|---|---|
| **SharedKernel exceptions** | `src/Shared/SharedKernel/Src/His.Hope.SharedKernel/Domain/Exceptions/` |
| **CorrelationContext** | `src/Shared/Infrastructure/His.Hope.Infrastructure/Observability/CorrelationContext.cs` |
| **CorrelationIdMiddleware** | `src/Shared/Infrastructure/His.Hope.Infrastructure/Middleware/CorrelationIdMiddleware.cs` |
| **GlobalExceptionMiddleware** | `src/Shared/Infrastructure/His.Hope.Infrastructure/Middleware/GlobalExceptionMiddleware.cs` |
| **GrpcGlobalExceptionInterceptor** | `src/Shared/Infrastructure/His.Hope.Infrastructure/Middleware/GrpcGlobalExceptionInterceptor.cs` |
| **TracingBehaviour** | `src/Shared/Infrastructure/His.Hope.Infrastructure/Observability/TracingBehaviour.cs` |
| **MiddlewareExtensions** | `src/Shared/Infrastructure/His.Hope.Infrastructure/Middleware/MiddlewareExtensions.cs` |
| **DependencyInjection** | `src/Shared/Infrastructure/His.Hope.Infrastructure/DependencyInjection.cs` |
| **OpenTelemetryExtensions** | `src/Shared/Infrastructure/His.Hope.Infrastructure/Observability/OpenTelemetryExtensions.cs` |
| **ErrorService (Angular)** | `src/Frontend/his-hope-app/src/app/core/services/error.service.ts` |
| **GlobalErrorHandler (Angular)** | `src/Frontend/his-hope-app/src/app/core/errors/global-error-handler.ts` |
| **Error Actions** | `src/Frontend/his-hope-app/src/app/store/error/error.actions.ts` |
| **Error Reducer** | `src/Frontend/his-hope-app/src/app/store/error/error.reducer.ts` |
| **Error Selectors** | `src/Frontend/his-hope-app/src/app/store/error/error.selectors.ts` |
| **ErrorInterceptor** | `src/Frontend/his-hope-app/src/app/core/interceptors/error.interceptor.ts` |
| **ErrorBarComponent** | `src/Frontend/his-hope-app/src/app/shared/components/error-bar/error-bar.component.ts` |
| **Prometheus rules** | `k8s/monitoring/prometheus-rules.yaml` |
| **AlertManager config** | `k8s/monitoring/alertmanager-config.yaml` |
| **Grafana dashboards** | `k8s/monitoring/grafana-dashboards.yaml` |
| **Docker Prometheus** | `docker/prometheus.yml` |

---

## 2. Correlation ID

### What is Correlation ID?

A `X-Correlation-Id` is a unique identifier attached to every request entering the system. It flows from the frontend through all backend services, enabling complete request tracing across service boundaries.

### How It Works

**Backend (`CorrelationIdMiddleware`):**
1. Reads `X-Correlation-Id` from the incoming HTTP request header
2. If missing, generates a 12-character hex ID via `Guid.NewGuid().ToString("N")[..12]`
3. Stores the ID in `CorrelationContext.CurrentId` (async-local scoped)
4. Sets the `X-Correlation-Id` response header on the outgoing response

```csharp
// src/Shared/Infrastructure/His.Hope.Infrastructure/Middleware/CorrelationIdMiddleware.cs
public async Task InvokeAsync(HttpContext context)
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();

    if (string.IsNullOrEmpty(correlationId))
    {
        correlationId = Guid.NewGuid().ToString("N")[..12];
    }

    CorrelationContext.CurrentId = correlationId;

    context.Response.OnStarting(() =>
    {
        if (!context.Response.Headers.ContainsKey("X-Correlation-Id"))
        {
            context.Response.Headers["X-Correlation-Id"] = correlationId;
        }
        return Task.CompletedTask;
    });

    await _next(context);
}
```

**CorrelationContext (`AsyncLocal`):**
```csharp
// src/Shared/Infrastructure/His.Hope.Infrastructure/Observability/CorrelationContext.cs
public class CorrelationContext
{
    private static readonly AsyncLocal<string> _correlationId = new();

    public static string CurrentId
    {
        get => _correlationId.Value ?? "unknown";
        set => _correlationId.Value = value;
    }

    public bool HasCorrelationId => !string.IsNullOrEmpty(_correlationId.Value);
}
```

**Frontend (`ErrorService`):**
The Angular `ErrorService.getCorrelationId()` extracts the correlation ID from:
1. `X-Correlation-Id` response header
2. `error.error.correlationId` body field (ProblemDetails)
3. Falls back to a generated ID (`hh-{timestamp}-{random}`)

```typescript
// src/Frontend/his-hope-app/src/app/core/services/error.service.ts
getCorrelationId(error: HttpErrorResponse): string {
    const fromHeaders = error.headers?.get('X-Correlation-Id');
    if (fromHeaders) return fromHeaders;

    if (typeof error.error === 'object' && error.error?.correlationId) {
      return error.error.correlationId;
    }

    return this.generateCorrelationId();
}
```

### Flow Diagram

```
Frontend                    Gateway                   IdentityService           PatientService
  │                          │                          │                        │
  │  POST /api/patients      │                          │                        │
  │  X-Correlation-Id: abc   │                          │                        │
  │─────────────────────────►│                          │                        │
  │                          │  POST /api/v1/patients   │                        │
  │                          │  X-Correlation-Id: abc   │                        │
  │                          │─────────────────────────►│                        │
  │                          │                          │  gRPC CheckPatient     │
  │                          │                          │  X-Correlation-Id: abc │
  │                          │                          │───────────────────────►│
  │                          │                          │                        │
  │                          │                          │◄───────────────────────┤
  │                          │  ProblemDetails 404      │                        │
  │                          │  X-Correlation-Id: abc   │                        │
  │                          │◄─────────────────────────┤                        │
  │                          │                          │                        │
  │◄─────────────────────────┤                          │                        │
  │                          │                          │                        │
```

### Correlation ID in Logs

Every log entry includes the correlation ID, enabling full trace reconstruction in Kibana:

```json
{
  "@timestamp": "2026-07-17T12:00:00.123Z",
  "level": "Error",
  "service": "patient-service",
  "traceId": "0a1b2c3d4e5f6789",
  "correlationId": "abc123def456",
  "messageTemplate": "Unhandled exception occurred: {Message}"
}
```

---

## 3. Exception Handling

### Exception Types and Mappings

| Exception | HTTP Status | gRPC Status | ProblemDetails `type` | Source File |
|---|---|---|---|---|
| `FluentValidation.ValidationException` | 400 Bad Request | `InvalidArgument` | `validation-error` | FluentValidation |
| `DomainException` | 422 Unprocessable Entity | `FailedPrecondition` | `domain-error` | `SharedKernel/Domain/Exceptions/DomainException.cs` |
| `NotFoundException` | 404 Not Found | `NotFound` | `not-found` | `SharedKernel/Domain/Exceptions/NotFoundException.cs` |
| `UnauthorizedException` | 401 Unauthorized | `Unauthenticated` | `unauthorized` | `SharedKernel/Domain/Exceptions/UnauthorizedException.cs` |
| `ForbiddenException` | 403 Forbidden | `PermissionDenied` | `forbidden` | `SharedKernel/Domain/Exceptions/ForbiddenException.cs` |
| Any unhandled `Exception` | 500 Internal Server Error | `Internal` | `internal-server-error` | N/A |
| `RpcException` | N/A | (passthrough) | N/A | gRPC infrastructure |

### HTTP Mapping (`GlobalExceptionMiddleware`)

```csharp
// src/Shared/Infrastructure/His.Hope.Infrastructure/Middleware/GlobalExceptionMiddleware.cs
private static (int statusCode, string title, string type) MapException(Exception exception)
{
    return exception switch
    {
        ValidationException => (400, "Bad Request", "validation-error"),
        DomainException     => (422, "Unprocessable Entity", "domain-error"),
        NotFoundException   => (404, "Not Found", "not-found"),
        UnauthorizedException => (401, "Unauthorized", "unauthorized"),
        ForbiddenException  => (403, "Forbidden", "forbidden"),
        _ => (500, "Internal Server Error", "internal-server-error")
    };
}
```

### gRPC Mapping (`GrpcGlobalExceptionInterceptor`)

```csharp
// src/Shared/Infrastructure/His.Hope.Infrastructure/Middleware/GrpcGlobalExceptionInterceptor.cs
public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
    TRequest request, ServerCallContext context,
    UnaryServerMethod<TRequest, TResponse> continuation)
{
    try
    {
        return await continuation(request, context);
    }
    catch (DomainException ex)
    {
        throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
    }
    catch (NotFoundException ex)
    {
        throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
    }
    catch (ValidationException ex)
    {
        throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
    }
    catch (UnauthorizedException ex)
    {
        throw new RpcException(new Status(StatusCode.Unauthenticated, ex.Message));
    }
    catch (ForbiddenException ex)
    {
        throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
    }
    catch (RpcException) { throw; }
    catch (Exception)
    {
        throw new RpcException(new Status(StatusCode.Internal, "Internal error"));
    }
}
```

### Service-Level Exception Middleware

Services that have their own `ExceptionHandlingMiddleware` (PatientService, ClinicalService, LabService, PharmacyService, BillingService) were updated to resolve the `NotFoundException` ambiguity by using the fully-qualified reference:

```csharp
using NotFoundException = His.Hope.SharedKernel.Domain.Exceptions.NotFoundException;
```

This ensures all services consistently map `SharedKernel.NotFoundException` instead of framework-level `NotFoundException` types.

---

## 4. ProblemDetails Format

All HTTP error responses follow [RFC 7807](https://tools.ietf.org/html/rfc7807) ProblemDetails format with the following schema:

### Response Schema

| Field | Type | Description | Always Present |
|---|---|---|---|
| `type` | string | URI identifying the error type | Yes |
| `title` | string | Short human-readable error title | Yes |
| `status` | int | HTTP status code | Yes |
| `detail` | string | Detailed error message (sensitive info excluded for 500s) | Yes |
| `instance` | string | URL that caused the error | Yes |
| `traceId` | string | OpenTelemetry trace ID for Jaeger lookup | Yes |
| `correlationId` | string | Correlation ID for Kibana log lookup | Yes |
| `timestamp` | string | UTC ISO 8601 timestamp | Yes |

### Example Responses

**400 Validation Error:**
```json
{
  "type": "https://his-hope.com/errors/validation-error",
  "title": "Bad Request",
  "status": 400,
  "detail": "'First Name' must not be empty; 'Last Name' must not be empty",
  "instance": "http://localhost:5002/api/v1/patients",
  "traceId": "0a1b2c3d4e5f6789",
  "correlationId": "abc123def456",
  "timestamp": "2026-07-17T12:00:00.123Z"
}
```

**404 Not Found:**
```json
{
  "type": "https://his-hope.com/errors/not-found",
  "title": "Not Found",
  "status": 404,
  "detail": "Patient with ID 1001 not found",
  "instance": "http://localhost:5002/api/v1/patients/1001",
  "traceId": "0a1b2c3d4e5f6789",
  "correlationId": "abc123def456",
  "timestamp": "2026-07-17T12:00:00.123Z"
}
```

**500 Internal Server Error:**
```json
{
  "type": "https://his-hope.com/errors/internal-server-error",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "An unexpected error occurred",
  "instance": "http://localhost:5002/api/v1/patients",
  "traceId": "0a1b2c3d4e5f6789",
  "correlationId": "abc123def456",
  "timestamp": "2026-07-17T12:00:00.123Z"
}
```

### ProblemDetails Implementation

```csharp
// src/Shared/Infrastructure/His.Hope.Infrastructure/Middleware/GlobalExceptionMiddleware.cs
private async Task HandleExceptionAsync(HttpContext context, Exception exception)
{
    var (statusCode, title, type) = MapException(exception);

    if (statusCode >= 500)
        _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);
    else
        _logger.LogWarning(exception, "Request failed with status {StatusCode}: {Message}", statusCode, exception.Message);

    context.Response.ContentType = "application/problem+json";
    context.Response.StatusCode = statusCode;

    var detail = exception switch
    {
        ValidationException ve => string.Join("; ", ve.Errors.Select(e => e.ErrorMessage)),
        _ => exception.Message
    };

    var problemDetails = new
    {
        type = $"https://his-hope.com/errors/{type}",
        title,
        status = statusCode,
        detail,
        instance = context.Request.GetDisplayUrl(),
        traceId = Activity.Current?.TraceId.ToString() ?? "unknown",
        correlationId = CorrelationContext.CurrentId,
        timestamp = DateTime.UtcNow.ToString("o")
    };

    var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    await context.Response.WriteAsync(json);
}
```

---

## 5. Distributed Tracing

### OpenTelemetry Pipeline

```
┌──────────────┐    ┌──────────────────┐    ┌──────────────────┐    ┌────────────┐
│  .NET Service │───►│ OpenTelemetry SDK│───►│   Jaeger Agent   │───►│   Jaeger   │
│               │    │  (auto-instr.)   │    │   (DaemonSet)    │    │  (port     │
│               │    │                  │    │                  │    │   16686)   │
│  - ASP.NET   │    │  - Traces        │    │                  │    │            │
│  - HttpClient│    │  - Metrics       │    │                  │    │            │
│  - EF Core   │    │  - Logs          │    │                  │    │            │
│  - MediatR   │    └──────┬───────────┘    └──────────────────┘    └────────────┘
│               │           │
│  - correlation│    ┌──────▼───────────┐
│    .id tag    │    │   Prometheus     │
└──────────────┘    │   (port 9090)    │
                     └──────────────────┘
```

### Trace Context Propagation

OpenTelemetry uses the [W3C Trace Context](https://www.w3.org/TR/trace-context/) standard (`traceparent` header) to propagate trace context across HTTP and gRPC calls:

```
Frontend                             PatientService                    ClinicalService
  │                                      │                                 │
  │ POST /api/v1/patients               │                                 │
  │ traceparent: 00-abc...-def...-01    │                                 │
  │────────────────────────────────────►│                                 │
  │                                      │ gRPC GetEncounterHistory        │
  │                                      │ traceparent: 00-abc...-ghi...-01│
  │                                      │────────────────────────────────►│
  │                                      │                                 │
  │                                      │◄────────────────────────────────│
  │◄────────────────────────────────────│                                 │
```

### Span Enrichment

The `OpenTelemetryExtensions.cs` enriches every span with `correlation.id` from the request header:

```csharp
// src/Shared/Infrastructure/His.Hope.Infrastructure/Observability/OpenTelemetryExtensions.cs
options.EnrichWithHttpRequest = (activity, request) =>
{
    activity.SetTag("http.method", request.Method);
    activity.SetTag("http.url", request.Path);
    activity.SetTag("correlation.id", request.Headers["X-Correlation-Id"].FirstOrDefault() ?? "unknown");
};
```

### Tracing Pipeline Instrumentation

| Instrumentation | Traces | Metrics | Source |
|---|---|---|---|
| ASP.NET Core | HTTP request spans | `http_server_duration_ms` | `AddAspNetCoreInstrumentation()` |
| HttpClient | Outbound HTTP/gRPC spans | `http_client_duration_ms` | `AddHttpClientInstrumentation()` |
| EF Core | Database query spans | `db_query_duration_ms` | `AddEntityFrameworkCoreInstrumentation()` |
| MediatR (TracingBehaviour) | Command/Query spans | — | Custom `IPipelineBehavior` |
| Runtime | — | GC, CPU, memory, thread pool | `AddRuntimeInstrumentation()` |
| Process | — | Process CPU, memory | `AddProcessInstrumentation()` |

---

## 6. MediatR Tracing

The `TracingBehaviour` is a MediatR `IPipelineBehavior<,>` that creates an OpenTelemetry span for every command or query passing through the pipeline.

### Implementation

```csharp
// src/Shared/Infrastructure/His.Hope.Infrastructure/Observability/TracingBehaviour.cs
public class TracingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly ActivitySource _source = new("His.Hope.MediatR");

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        using var activity = _source.StartActivity(name, ActivityKind.Internal);
        activity?.SetTag("request.type", name);
        activity?.SetTag("correlation.id", CorrelationContext.CurrentId);

        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.message", ex.Message);
            activity?.SetTag("exception.stacktrace", ex.ToString());
            throw;
        }
    }
}
```

### Registration

Registered in `DependencyInjection.cs` as a scoped pipeline behavior:

```csharp
// src/Shared/Infrastructure/His.Hope.Infrastructure/DependencyInjection.cs
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TracingBehaviour<,>));
```

### Example Trace in Jaeger

```
Span: POST /api/v1/patients [trace_id=abc123]
  ├── CreatePatientCommand.Handle [span_id=def456]
  │     ├── db.save (PatientDbContext.SaveChangesAsync) [12ms]
  │     │     ├── outbox.create (OutboxDomainEventInterceptor) [1ms]
  │     │     └── db.commit (CockroachDB) [11ms]
  │     └── cache.remove (Redis Cluster) [2ms]
  ├── eventbus.publish (PatientRegisteredIntegrationEvent) [15ms]
  │     └── rabbitmq.publish (Exchange: his_hope_patient) [12ms]
  └── cache.remove (patients:search:prefix) [1ms]
```

Each span is tagged with:
- `request.type` — the CQRS command/query class name
- `correlation.id` — the request's correlation ID
- `exception.message` / `exception.stacktrace` — if the handler threw

---

## 7. Frontend Error Handling

### Architecture

```
HttpErrorResponse / Error
        │
        ▼
┌──────────────────┐
│  ErrorInterceptor │  ← Retry for 503/504, extract X-Correlation-Id
│  (HTTP interceptor)│    route to login on 401, show snackbar
└────────┬─────────┘
         │
    ┌────▼────┐
    │  Error  │
    │ Service │  ← buildErrorContext(), getCorrelationId(), reportError()
    └────┬────┘
         │
    ┌────▼──────────────┐
    │ GlobalErrorHandler │  ← Angular ErrorHandler, dispatches NgRx action
    │                    │    shows MatSnackBar
    └────┬──────────────┘
         │
    ┌────▼────┐     ┌───────────┐
    │ NgRx    │     │ ErrorBar  │
    │ Store   │────►│ Component │  ← Visual error bar with severity colors
    │ (error) │     │           │    copy reference ID button
    └─────────┘     └───────────┘
```

### ErrorInterceptor

Located at `src/Frontend/his-hope-app/src/app/core/interceptors/error.interceptor.ts`:

- **Retry logic**: Retries HTTP requests once for 503/504 status codes (transient failures)
- **401 handling**: Clears stored token, redirects to login (unless already on `/auth/` route)
- **403 handling**: Shows "Access denied" snackbar
- **422 handling**: Shows validation error message
- **429 handling**: Shows rate limit notification
- **500+ handling**: Shows error with correlation ID reference
- **Network errors (status 0)**: Shows persistent network error snackbar
- **Skip list**: URLs matching `/auth/verify` and `/auth/me` skip notifications

### ErrorService

Located at `src/Frontend/his-hope-app/src/app/core/services/error.service.ts`:

- `buildErrorContext(error)` — normalizes any error type (HttpErrorResponse, Error, string) into a uniform `ErrorContext`
- `getCorrelationId(error)` — extracts from headers, body, or generates fallback
- `reportError(context)` — POSTs error context to backend `/api/v1/errors` endpoint
- `trackUserAction(action)` — stores the current user action name for context

```typescript
export interface ErrorContext {
  correlationId: string;
  message: string;
  type: string;
  stack?: string;
  url: string;
  userAction?: string;
  timestamp: string;
  userId?: string;
}
```

### GlobalErrorHandler

Located at `src/Frontend/his-hope-app/src/app/core/errors/global-error-handler.ts`:

- Implements Angular `ErrorHandler` — catches all uncaught errors
- Builds error context via `ErrorService`
- Reports errors to backend (unless type is `UNKNOWN`)
- Shows `MatSnackBar` with severity-appropriate message and duration
- Dispatches `captureError` NgRx action with message, code, and correlationId
- Auto-clears error from NgRx store after 8 seconds

### NgRx Error State

**Actions** (`src/Frontend/his-hope-app/src/app/store/error/error.actions.ts`):
```typescript
export const captureError = createAction(
  '[Error] Capture',
  props<ErrorPayload>(),
);
export const clearError = createAction('[Error] Clear');
```

**Reducer** (`src/Frontend/his-hope-app/src/app/store/error/error.reducer.ts`):
```typescript
export interface ErrorState {
  message: string | null;
  code: string | null;
  correlationId: string | null;
  timestamp: string | null;
}
```

**Selectors** (`src/Frontend/his-hope-app/src/app/store/error/error.selectors.ts`):
```typescript
export const selectError = createSelector(selectErrorState, (state) => state);
export const selectErrorMessage = createSelector(selectErrorState, (state) => state.message);
export const selectErrorCorrelationId = createSelector(selectErrorState, (state) => state.correlationId);
```

### ErrorBarComponent

Located at `src/Frontend/his-hope-app/src/app/shared/components/error-bar/error-bar.component.ts`:

- Reads error from NgRx store via `selectError`
- Visual severity classes: `error-bar--HTTP_5XX` (red), `error-bar--HTTP_4XX` (orange), `error-bar--UNKNOWN` (grey)
- Severity-appropriate icons: `cloud_off` (5xx), `warning_amber` (4xx), `wifi_off` (network), `error_outline` (default)
- Shows correlation ID reference with one-click copy button
- Auto-dismisses after 8 seconds
- Manual dismiss button

---

## 8. Alerting

### Prometheus Alert Rules

Defined in `k8s/monitoring/prometheus-rules.yaml`. Two alert groups:

#### SLO Alerts (`his-hope.slo.alerts`)

| Alert | Description | Severity | Expression |
|---|---|---|---|
| **SLOErrorBudgetBurnCritical** | Error budget burn rate >2x over 5m and 1h windows | critical | Multi-window burn rate |
| **SLOErrorBudgetBurnWarning** | Error budget burn rate >1x over 6h | warning | Single-window burn rate |
| **HighLatencyP99** | p99 latency >500ms for 5m | warning | `histogram_quantile(0.99, ...)` |
| **ServiceDown** | Service scrape target missing for 30s | critical | `absent(up{job=~".+-service"})` |
| **HighGrpcErrorRate** | gRPC error rate >5% for 5m | warning | `grpc_server_handled_total` |
| **CircuitBreakerOpen** | Circuit breaker in OPEN state for 1m | critical | `circuit_breaker_state{state="open"}` |
| **OutboxBacklogGrowing** | Pending outbox messages accumulating | warning | `rate(outbox_messages_total)` |
| **RabbitMQQueueDepth** | Queue depth >1000 for 5m | warning | `rabbitmq_queue_messages_ready` |

#### Business Error Alerts (`business-errors`)

| Alert | Description | Severity | Expression |
|---|---|---|---|
| **HighErrorRate** | HTTP 5xx rate >5% for 2m | critical | `rate(http_requests_total{status=~"5.."}[5m]) > 0.05` |
| **ApiLatencyHigh** | p99 latency >2s for 5m | warning | `histogram_quantile(0.99, ...) > 2000` |
| **ServiceDown** | Service `up == 0` for 1m | critical | `up == 0` |
| **DeadLetterQueueGrowth** | DLQ messages >100 for 5m | warning | `rabbitmq_queue_messages{queue=~".*deadletter.*"} > 100` |
| **HighMemoryUsage** | Container memory >90% for 5m | warning | `container_memory_usage_bytes / container_spec_memory_limit_bytes > 0.9` |

### AlertManager Configuration

Located at `k8s/monitoring/alertmanager-config.yaml`:

#### Routing

```
All Alerts
  │
  ├── severity=critical
  │     ├── Receiver: sre-team-critical (PagerDuty + Slack #his-hope-critical)
  │     ├── Receiver: slack-critical (Slack #his-hope-alerts, custom webhook)
  │     └── Receiver: email-oncall (oncall@his-hope.com)
  │
  └── severity=warning
        └── Receiver: sre-team (Slack #his-hope-alerts)
```

#### Receivers

| Receiver | Channel | Configuration |
|---|---|---|
| `sre-team` | Slack `#his-hope-alerts` | Standard webhook, includes description |
| `sre-team-critical` | PagerDuty + Slack `#his-hope-critical` | High-urgency PagerDuty + critical Slack |
| `slack-critical` | Slack `#his-hope-alerts` | Custom webhook, includes runbook URL |
| `email-oncall` | Email `oncall@his-hope.com` | SMTP via `smtp.his-hope.com:587` |

#### Configuration Guide

To configure AlertManager receivers:

1. **Slack webhook URL**: Set `SLACK_WEBHOOK_URL` environment variable in the alertmanager deployment, or replace the placeholder `api_url` in `alertmanager-config.yaml`
2. **PagerDuty**: Replace `routing_key: 'xxxxx'` with your PagerDuty integration key
3. **Email SMTP**: Update `smtp.his-hope.com:587` with your SMTP relay, and `alertmanager@his-hope.com` / `oncall@his-hope.com` with actual addresses

```yaml
# Example: Configuring a Slack receiver
- name: 'slack-critical'
  slack_configs:
  - api_url: 'https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXXXXXX'
    channel: '#his-hope-alerts'
    title: '{{ .GroupLabels.alertname }}'
    text: |
      *Severity:* {{ .CommonLabels.severity }}
      *Alert:* {{ .CommonAnnotations.summary }}
      *Runbook:* {{ .CommonAnnotations.runbook_url }}
    send_resolved: true
```

#### Docker Compose Prometheus Alerting

`docker/prometheus.yml` was updated to include the alerting section:

```yaml
alerting:
  alertmanagers:
    - static_configs:
        - targets: ['alertmanager:9093']

rule_files:
  - "/etc/prometheus/rules/*.yml"
```

---

## 9. Grafana Dashboard

### Error Tracking & Tracing Dashboard

Defined in `k8s/monitoring/grafana-dashboards.yaml` as `grafana-dashboard-error-tracking`.

#### Dashboard Panels

| ID | Panel Title | Type | Data Source | Description |
|---|---|---|---|---|
| 1 | **Error Rate by Service** | Time Series | Prometheus | 5xx rate per service over time |
| 2 | **Top 5 Error Types** | Bar Gauge | Prometheus | Most frequent error status codes |
| 3 | **Request Latency p50 / p95 / p99** | Time Series | Prometheus | Latency percentiles across all services |
| 4 | **Active Alerts** | Table | Prometheus | Currently firing alerts |
| 5 | **Failed gRPC Calls by Method** | Time Series | Prometheus | gRPC error rate per method |
| 6 | **DLQ Message Count** | Stat | Prometheus | Total dead-letter queue messages |
| 7 | **Errors vs Latency Correlation** | Time Series | Prometheus | Overlay of error rate + p99 latency |

### Panel Details

**Panel 1 — Error Rate by Service:**
```promql
sum(rate(http_requests_total{status=~"5.."}[5m])) by (service)
```

**Panel 4 — Active Alerts:**
```promql
ALERTS{alertstate="firing"}
```

**Panel 6 — DLQ Message Count (with thresholds):**
```promql
sum(rabbitmq_queue_messages{queue=~".*deadletter.*"})
```
- Green: 0-50
- Yellow: 50-100
- Red: >100

**Panel 7 — Errors vs Latency Correlation:**
```promql
# Error rate (left axis)
rate(http_requests_total{status=~"5.."}[5m])
# p99 latency (right axis)
histogram_quantile(0.99, sum(rate(http_request_duration_seconds_bucket[5m])) by (le, service))
```

---

## 10. Adding a New Service

### Checklist for Integrating Error Handling

#### 1. Add SharedKernel NuGet Reference

Ensure the new service project references the `His.Hope.SharedKernel` and `His.Hope.Infrastructure` projects.

#### 2. Register Enterprise Infrastructure

In `Program.cs`, call `AddHisHopeEnterpriseInfrastructure()`:

```csharp
builder.Services.AddHisHopeEnterpriseInfrastructure(
    builder.Configuration,
    "new-service-name",
    builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379"));
```

This registers:
- OpenTelemetry with Jaeger and Prometheus exporters
- Redis caching
- PHI audit service
- `CorrelationContext` (scoped)
- `GlobalExceptionMiddleware` (singleton)
- `TracingBehaviour` (MediatR pipeline)

#### 3. Add Middleware Pipeline

```csharp
var app = builder.Build();

app.UseCorrelationId();
app.UseGlobalExceptionHandler();

// ... other middleware (auth, routing, etc.)
```

#### 4. Add gRPC Exception Interceptor

If the service hosts gRPC endpoints, register the interceptor:

```csharp
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<GrpcGlobalExceptionInterceptor>();
});
```

#### 5. Remove or Update Service-Level Exception Middleware

If the service has its own `ExceptionHandlingMiddleware`, either:
- Remove it (the shared global middleware handles all cases)
- Or update it to use the fully-qualified `SharedKernel.Domain.Exceptions.NotFoundException` to avoid ambiguity

```csharp
// Fix NotFoundException ambiguity in service-level middleware:
using NotFoundException = His.Hope.SharedKernel.Domain.Exceptions.NotFoundException;
```

#### 6. Add Prometheus Scrape Target

Add the service to `docker/prometheus.yml`:

```yaml
- job_name: 'newservice'
  metrics_path: '/metrics'
  static_configs:
    - targets: ['newservice:PORT']
```

Also add to `k8s/monitoring/prometheus-rules.yaml` if custom alert rules are needed.

#### 7. Add Alert Rules (if needed)

Create service-specific alert rules in the `business-errors` group or add the service to existing alert expressions.

#### 8. Verify

- [ ] Request with `X-Correlation-Id` header returns `X-Correlation-Id` in response
- [ ] Request without header gets auto-generated correlation ID
- [ ] Throwing `NotFoundException` returns ProblemDetails 404 with `correlationId` and `traceId`
- [ ] Throwing `ValidationException` returns ProblemDetails 400 with validation details
- [ ] Jaeger shows spans for each MediatR command/query
- [ ] Logs contain `correlationId` and `traceId` fields
- [ ] Prometheus metrics (`http_requests_total`, `http_server_duration_ms`) appear for the new service
- [ ] Frontend ErrorInterceptor shows appropriate snackbar for error status codes
- [ ] Grafana Error Tracking dashboard shows new service data

---

## Troubleshooting

### Problem: Response has no `X-Correlation-Id` header

**Check:**
1. Is `app.UseCorrelationId()` called before `app.UseRouting()` in `Program.cs`?
2. Is `CorrelationContext` registered in DI (`services.AddScoped<CorrelationContext>()`)?

### Problem: ProblemDetails shows `traceId: "unknown"`

**Check:**
1. Is OpenTelemetry configured (`AddHisHopeOpenTelemetry`)?
2. Is the Jaeger exporter running? Check `docker ps | findstr jaeger`.

### Problem: gRPC calls return wrong status code

**Check:**
1. Is `GrpcGlobalExceptionInterceptor` registered in the gRPC service options?
2. Are exception types matching the expected mapping (e.g., `NotFoundException` → `NotFound`)?

### Problem: Frontend shows generic error without correlation ID

**Check:**
1. Is `ErrorInterceptor` registered in `app.module.ts` providers?
2. Is `GlobalErrorHandler` registered as the Angular `ErrorHandler` provider?
3. Does the backend response include the `X-Correlation-Id` header?

### Problem: Alerts not firing

**Check:**
1. Is Prometheus scraping the target? `http://localhost:9090/targets`
2. Are rule files mounted correctly? Check `docker/prometheus.yml` `rule_files` path.
3. Is AlertManager configured as a Prometheus alerting target? Check `alerting.alertmanagers`.

### Problem: No error data in Grafana Error Tracking dashboard

**Check:**
1. Is the Prometheus datasource configured in Grafana?
2. Are the `http_requests_total` and `http_request_duration_seconds_bucket` metrics exposed?
3. Does the service export metrics at `/metrics` endpoint?

---

> **Last updated**: 2026-07-17 | **Maintainer**: @architect | **Next review**: 2026-08-17
