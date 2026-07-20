using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using MediatR;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using His.Hope.AgentHarness.Mcp;
using His.Hope.AgentHarness.Mcp.Tools;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Infrastructure.Persistence;
using His.Hope.AgentHarness.Infrastructure.Dispatch;
using His.Hope.AgentHarness.Infrastructure.Observability;
using Microsoft.EntityFrameworkCore;
using His.Hope.AgentHarness.Infrastructure.EventBus;
using His.Hope.AgentHarness.Infrastructure.Temporal;
using His.Hope.AgentHarness.Application.Behaviors;
using His.Hope.AgentHarness.Application.Commands.StartPipeline;
using His.Hope.AgentHarness.Application.Interfaces;
using His.Hope.AgentHarness.Application.Services;
using His.Hope.AgentHarness.Core.Models;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

try
{
    if (args.Contains("--stdio"))
    {
        await RunStdioMode();
    }
    else
    {
        await RunHttpMode(args);
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "Agent Harness MCP Server terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// ============================================================
// HTTP MODE (Kestrel web server — default)
// ============================================================
static async Task RunHttpMode(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
        ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("His.Hope.AgentHarness"))
        .WithTracing(tracer =>
        {
            tracer
                .AddAspNetCoreInstrumentation()
                .AddSource("His.Hope.AgentHarness");

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                tracer.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
            }
            else
            {
                tracer.AddConsoleExporter();
            }
        })
        .WithMetrics(meter =>
        {
            meter
                .AddAspNetCoreInstrumentation()
                .AddMeter("His.Hope.AgentHarness")
                .AddPrometheusExporter();

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                meter.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
            }
        });

    var config = builder.Configuration
        .GetSection("AgentHarness")
        .Get<McpServerConfig>() ?? new McpServerConfig();

    ConfigureServices(builder.Services, config);

    var app = builder.Build();

    // Auto-create database schema
    using (var scope = app.Services.CreateScope())
    {
        await InitializeDatabase(scope.ServiceProvider);
    }

    // SSE sessions: maps sessionId -> Channel<string> for sending
    // JSON-RPC responses back through the SSE stream
    var sseSessions = new ConcurrentDictionary<string, Channel<string>>();

    // Health endpoint
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "agent-harness" }));
    app.MapGet("/health/ready", () => Results.Ok(new { status = "ready", service = "agent-harness" }));
    app.MapGet("/health/startup", () => Results.Ok(new { status = "started", service = "agent-harness" }));

    // Prometheus metrics endpoint
    app.MapPrometheusScrapingEndpoint();

    // 1. SSE Endpoint: GET /mcp/sse
    app.MapGet("/mcp/sse", async (HttpContext ctx, CancellationToken ct) =>
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
        sseSessions[sessionId] = channel;

        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers.CacheControl = "no-cache";
        ctx.Response.Headers.Connection = "keep-alive";
        ctx.Response.Headers["X-Accel-Buffering"] = "no";

        Log.Information("MCP SSE session started: {SessionId}", sessionId);

        try
        {
            // Send endpoint event (tells client where to POST messages)
            await ctx.Response.WriteAsync($"event: endpoint\n", ct);
            await ctx.Response.WriteAsync($"data: /mcp?sessionId={sessionId}\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);

            // Read from channel and write to SSE stream
            var heartbeatInterval = TimeSpan.FromSeconds(15);
            var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var readTask = Task.Run(async () =>
            {
                await foreach (var message in channel.Reader.ReadAllAsync(heartbeatCts.Token))
                {
                    await ctx.Response.WriteAsync($"event: message\n", ct);
                    await ctx.Response.WriteAsync($"data: {message}\n\n", ct);
                    await ctx.Response.Body.FlushAsync(ct);
                }
            }, ct);

            // Heartbeat loop to keep connection alive
            try
            {
                while (!heartbeatCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(heartbeatInterval, heartbeatCts.Token);
                    await ctx.Response.WriteAsync($": heartbeat\n\n", ct);
                    await ctx.Response.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException) { }

            await readTask;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning(ex, "MCP SSE session {SessionId} error", sessionId);
        }
        finally
        {
            sseSessions.TryRemove(sessionId, out _);
            channel.Writer.TryComplete();
            Log.Information("MCP SSE session ended: {SessionId}", sessionId);
        }
    });

    // 2. Messages Endpoint: POST /mcp?sessionId=...
    app.MapPost("/mcp", async (HttpContext ctx) =>
    {
        var sessionId = ctx.Request.Query["sessionId"].FirstOrDefault();
        if (string.IsNullOrEmpty(sessionId) || !sseSessions.TryGetValue(sessionId, out var channel))
        {
            // Fallback: stateless JSON-RPC (no SSE session)
            var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
            var result = await HandleJsonRpcString(body, null, app.Services);
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = result != null ? 200 : 204;
            if (result != null) await ctx.Response.WriteAsync(result);
            return;
        }

        var bodyWithSession = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
        var resultWithSession = await HandleJsonRpcString(bodyWithSession, channel, app.Services);
        if (resultWithSession != null)
        {
            await channel.Writer.WriteAsync(resultWithSession);
        }
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync("{}");
    });

    // Also handle /mcp/messages for compatibility
    app.MapPost("/mcp/messages", async (HttpContext ctx) =>
    {
        var sessionId = ctx.Request.Query["sessionId"].FirstOrDefault();
        if (string.IsNullOrEmpty(sessionId) || !sseSessions.TryGetValue(sessionId, out var channel))
        {
            ctx.Response.StatusCode = 404;
            await ctx.Response.WriteAsync("{\"error\":\"Session not found\"}");
            return;
        }
        var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
        var result = await HandleJsonRpcString(body, channel, app.Services);
        if (result != null)
        {
            await channel.Writer.WriteAsync(result);
        }
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync("{}");
    });

    // Helper: return raw JSON without double-encoding
    static IResult RawJson(string json) => Results.Content(json, "application/json", System.Text.Encoding.UTF8);

    // SECURITY: API key authentication middleware (skip if no key configured)
    app.UseMiddleware<ApiKeyMiddleware>();

    // 3. REST endpoints
    app.MapPost("/mcp/start-pipeline", async (StartPipelineTool tool, HttpContext ctx) =>
    {
        try
        {
            var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
            return RawJson(await tool.ExecuteAsync(p));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "StartPipeline failed");
            return Results.Json(new { error = ex.Message, innerError = ex.InnerException?.Message ?? "", type = ex.GetType().Name }, statusCode: 500);
        }
    });

    app.MapPost("/mcp/get-status", async (GetStatusTool tool, HttpContext ctx) =>
    {
        try
        {
            var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
            return RawJson(await tool.ExecuteAsync(p));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetStatus failed");
            return Results.Json(new { error = ex.Message }, statusCode: 404);
        }
    });

    app.MapPost("/mcp/dispatch-agent", async (DispatchAgentTool tool, HttpContext ctx) =>
    {
        try
        {
            var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
            return RawJson(await tool.ExecuteAsync(p));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DispatchAgent failed");
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    });

    app.MapPost("/mcp/cancel-pipeline", async (CancelPipelineTool tool, HttpContext ctx) =>
    {
        try
        {
            var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
            return RawJson(await tool.ExecuteAsync(p));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CancelPipeline failed");
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    });

    app.MapPost("/mcp/get-pending-tasks", async (GetPendingTasksTool tool, HttpContext ctx) =>
    {
        try
        {
            var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
            return RawJson(await tool.ExecuteAsync(p));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetPendingTasks failed");
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    });

    app.MapPost("/mcp/complete-task", async (CompleteTaskTool tool, HttpContext ctx) =>
    {
        try
        {
            var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
            return RawJson(await tool.ExecuteAsync(p));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CompleteTask failed");
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    });

    app.MapPost("/mcp/get-pipeline-status", async (GetPipelineStatusTool tool, HttpContext ctx) =>
    {
        try
        {
            var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
            return RawJson(await tool.ExecuteAsync(p));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetPipelineStatus failed");
            return Results.Json(new { error = ex.Message }, statusCode: 404);
        }
    });

    app.MapPost("/mcp/get-pipeline-timeline", async (TimelineTool tool, HttpContext ctx) =>
    {
        try
        {
            var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
            return RawJson(await tool.ExecuteAsync(p));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetPipelineTimeline failed");
            return Results.Json(new { error = ex.Message }, statusCode: 404);
        }
    });

    app.MapPost("/mcp/save-artifact", async (SaveArtifactTool tool, HttpContext ctx) =>
    {
        try
        {
            var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
            return RawJson(await tool.ExecuteAsync(p));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SaveArtifact failed");
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    });

    app.MapPost("/mcp/get-artifact", async (GetArtifactTool tool, HttpContext ctx) =>
    {
        try
        {
            var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
            return RawJson(await tool.ExecuteAsync(p));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetArtifact failed");
            return Results.Json(new { error = ex.Message }, statusCode: 404);
        }
    });

    app.MapPost("/mcp/request-approval", async (RequestApprovalTool tool, HttpContext ctx) =>
    {
        try
        {
            var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
            return RawJson(await tool.ExecuteAsync(p));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "RequestApproval failed");
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    });

    app.MapPost("/mcp/approve-action", async (ApproveActionTool tool, HttpContext ctx) =>
    {
        try
        {
            var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
            return RawJson(await tool.ExecuteAsync(p));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ApproveAction failed");
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    });

    app.MapPost("/mcp/reject-action", async (RejectActionTool tool, HttpContext ctx) =>
    {
        try
        {
            var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
            return RawJson(await tool.ExecuteAsync(p));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "RejectAction failed");
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    });

    app.MapPost("/mcp/list-pending-approvals", async (ListPendingApprovalsTool tool, HttpContext ctx) =>
    {
        try
        {
            var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
            return RawJson(await tool.ExecuteAsync(p));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ListPendingApprovals failed");
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    });

    Log.Information("Agent Harness MCP Server starting on port {Port}", config.Port);

    // Resume any pipelines that were Running when the service stopped
    try
    {
        using (var startupScope = app.Services.CreateScope())
        {
            var startupStore = startupScope.ServiceProvider.GetRequiredService<IStateStore>();
            var running = await startupStore.GetRunningPipelinesAsync();

            foreach (var run in running)
            {
                var pipelineId = run.Id;
                var workflowId = run.WorkflowId;
                Log.Information("Queueing resume for pipeline {PipelineId} ({Workflow})", pipelineId, workflowId);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = app.Services.CreateScope();
                        var engine = scope.ServiceProvider.GetRequiredService<IPipelineEngine>();
                        var store = scope.ServiceProvider.GetRequiredService<IStateStore>();
                        using var bgCts = new CancellationTokenSource(TimeSpan.FromHours(8));

                        var pipelineRun = await store.GetPipelineRunAsync(pipelineId, bgCts.Token);
                        if (pipelineRun == null || pipelineRun.Status != PipelineStatus.Running) return;

                        Log.Information("Resuming pipeline {PipelineId} ({Workflow})", pipelineId, workflowId);
                        await engine.ResumeAsync(pipelineRun, bgCts.Token);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Resume failed for pipeline {PipelineId}", pipelineId);
                    }
                });
            }

            if (running.Count > 0)
                Log.Information("Scheduled resume for {Count} pipeline(s)", running.Count);
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Pipeline resume check failed (first start?)");
    }

    app.Run($"http://0.0.0.0:{config.Port}");
}

// ============================================================
// STDIO MODE (for OpenCode MCP local transport via docker exec)
// ============================================================
static async Task RunStdioMode()
{
    // In stdio mode, Serilog writes to stderr by default (console sink)
    // JSON-RPC responses go to stdout — they must not be mixed with logs.
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .CreateLogger();

    // Read config from environment variables (set in Docker Compose)
    var cfgBuilder = new ConfigurationBuilder()
        .AddJsonFile("/app/appsettings.json", optional: true)
        .AddEnvironmentVariables();
    var config = cfgBuilder.Build()
        .GetSection("AgentHarness")
        .Get<McpServerConfig>() ?? new McpServerConfig();

    var services = new ServiceCollection();
    ConfigureServices(services, config);
    var sp = services.BuildServiceProvider();

    // Auto-create database schema
    using (var scope = sp.CreateScope())
    {
        await InitializeDatabase(scope.ServiceProvider);
    }

    Log.Warning("Agent Harness MCP Server started in stdio mode");

    // Stdio MCP transport: newline-delimited JSON-RPC
    // Read line from stdin, process, write response line to stdout.
    using var stdin = Console.OpenStandardInput();
    using var stdout = Console.OpenStandardOutput();
    using var reader = new StreamReader(stdin);
    var writer = new StreamWriter(stdout) { AutoFlush = true };

    while (true)
    {
        var line = await reader.ReadLineAsync();
        if (line == null) break; // EOF

        // Skip empty lines
        if (string.IsNullOrWhiteSpace(line)) continue;

        var result = await HandleJsonRpcString(line, null, sp);
        if (result != null)
        {
            await writer.WriteLineAsync(result);
        }
    }
}

// ============================================================
// Shared DI Configuration
// ============================================================
static void ConfigureServices(IServiceCollection services, McpServerConfig config)
{
    services.AddHarnessPersistence(config.DatabaseConnectionString);
    services.AddSingleton<IEventBus>(sp =>
    {
        try
        {
            var bus = new RabbitMQEventBus(config.RabbitMQConnectionString);
            Log.Information("Connected to RabbitMQ at {RabbitMQ}", config.RabbitMQConnectionString);
            return bus;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to connect to RabbitMQ. Event bus disabled.");
            return new NullEventBus();
        }
    });
    services.AddSingleton<IAgentDispatcher, OpenCodeAgentDispatcher>();
    services.AddSingleton<WorkflowLoader>();

    // Register MediatR with handlers and pipeline behaviors
    services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(StartPipelineHandler).Assembly);
        cfg.AddOpenBehavior(typeof(CircuitBreakerBehavior<,>));
        cfg.AddOpenBehavior(typeof(RetryBehavior<,>));
        cfg.AddOpenBehavior(typeof(TimeoutBehavior<,>));
    });

    // Register pipeline engine and supporting services
    services.AddScoped(_ => new BackpressureController(config.MaxPipelineQueue, config.MaxAgentQueue));
    services.AddScoped<AgentPoolManager>();
    services.AddScoped<ErrorClassifier>();
    services.AddScoped<ConfidenceScorer>();
    services.AddScoped<ChangeScopeAnalyzer>();
    services.AddScoped<ConditionalDagBuilder>();
    services.AddScoped<LlmJudgeService>();
    services.AddScoped<EmbeddingService>();
    services.AddScoped<IMemoryService, MemoryService>();
    services.AddScoped<ILoopEngineer>(sp => new LoopEngineer(
        sp.GetRequiredService<ErrorClassifier>(),
        sp.GetRequiredService<ConfidenceScorer>(),
        sp.GetRequiredService<IMemoryService>(),
        sp.GetRequiredService<LlmJudgeService>(),
        config.LoopEngineerMaxIterations));
    // Temporal or local pipeline engine
    if (config.UseTemporal)
    {
        services.AddTemporalInfrastructure(config.TemporalServerUrl, useTemporal: true);
        Log.Information("Using Temporal pipeline engine at {Server}", config.TemporalServerUrl);
    }
    else
    {
        services.AddScoped<IPipelineEngine, PipelineEngine>();
    }

    services.AddSingleton<CostTracker>();
    services.AddSingleton<PromptTemplateService>();

    // Register MCP tools as scoped
    services.AddScoped<StartPipelineTool>();
    services.AddScoped<GetStatusTool>();
    services.AddScoped<DispatchAgentTool>();
    services.AddScoped<CancelPipelineTool>();
    services.AddScoped<GetPendingTasksTool>();
    services.AddScoped<CompleteTaskTool>();
    services.AddScoped<GetPipelineStatusTool>();
    services.AddScoped<SaveArtifactTool>();
    services.AddScoped<GetArtifactTool>();
    services.AddScoped<TimelineTool>();
    services.AddScoped<RequestApprovalTool>();
    services.AddScoped<ApproveActionTool>();
    services.AddScoped<RejectActionTool>();
    services.AddScoped<ListPendingApprovalsTool>();
    services.AddScoped<RecordInstinctTool>();
    services.AddScoped<QueryInstinctsTool>();
    services.AddScoped<IAgentMetricsService, AgentMetricsService>();
    services.AddSingleton<IAgentMetricsRecorder, HarnessMetricsRecorder>();
    services.AddScoped<GetAgentProfileTool>();
    services.AddScoped<InstinctOptimizer>();
    services.AddSingleton<GuardrailService>(sp =>
    {
        var costTracker = sp.GetRequiredService<CostTracker>();
        return new GuardrailService(costTracker);
    });
    services.AddSingleton<PiiRedactionService>();
    services.AddScoped<EvalEngineService>();
    services.AddScoped<EvaluateAgentTool>();
    services.AddScoped<CompareModelsTool>();
    services.AddScoped<RouteLlmTool>();
    services.AddScoped<AdaptiveQualityGates>();
}

static async Task InitializeDatabase(IServiceProvider sp)
{
    var db = sp.GetRequiredService<HarnessDbContext>();
    db.Database.ExecuteSqlRaw("CREATE EXTENSION IF NOT EXISTS vector");
    db.Database.ExecuteSqlRaw("ALTER TABLE IF EXISTS harness.memory_entries ADD COLUMN IF NOT EXISTS embedding vector(256)");
    db.Database.ExecuteSqlRaw("ALTER TABLE IF EXISTS harness.pipeline_runs ADD COLUMN IF NOT EXISTS parent_pipeline_run_id uuid");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS ix_pipeline_runs_parent_id ON harness.pipeline_runs (parent_pipeline_run_id)");
    db.Database.ExecuteSqlRaw("ALTER TABLE IF EXISTS harness.quality_gate_results ADD COLUMN IF NOT EXISTS gate_display_name character varying(256) NOT NULL DEFAULT ''");
    db.Database.ExecuteSqlRaw("ALTER TABLE IF EXISTS harness.quality_gate_results ADD COLUMN IF NOT EXISTS output text NULL");
    db.Database.ExecuteSqlRaw("ALTER TABLE IF EXISTS harness.quality_gate_results ADD COLUMN IF NOT EXISTS severity character varying(32) NOT NULL DEFAULT 'Block'");
    db.Database.ExecuteSqlRaw("""
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'harness'
                  AND table_name = 'quality_gate_results'
                  AND column_name = 'GateName') THEN
                ALTER TABLE harness.quality_gate_results ALTER COLUMN "GateName" DROP NOT NULL;
            END IF;

            IF EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'harness'
                  AND table_name = 'quality_gate_results'
                  AND column_name = 'Severity') THEN
                ALTER TABLE harness.quality_gate_results ALTER COLUMN "Severity" DROP NOT NULL;
            END IF;
        END $$;
        """);
    if (db.Database.GetMigrations().Any())
    {
        db.Database.Migrate();
    }
    else
    {
        Log.Warning("No EF Core migrations found for Agent Harness; falling back to EnsureCreated for local/dev compatibility.");
        db.Database.EnsureCreated();
    }
    // Seed default eval suite if none exist (ensures reproducible smoke testing)
    try
    {
        var store = sp.GetRequiredService<IStateStore>();
        var suites = await store.GetEvalSuitesAsync();
        if (suites.Count == 0)
        {
            var seedSuite = EvalSuite.Create(
                "dotnet-eval",
                "coding",
                "Default eval suite for .NET agent — generated outputs, deterministic pass/fail via stable hash.",
                """{"tasks":[{"input":"Write a function to add two numbers.","expected":"def add(a, b): return a + b"},{"input":"Write a SQL query to select all users.","expected":"SELECT * FROM users"},{"input":"Explain the concept of dependency injection.","expected":"Dependency injection is a design pattern where dependencies are passed into an object rather than created internally."}]}""");
            await store.SaveEvalSuiteAsync(seedSuite);
            Log.Information("Seeded default eval suite 'dotnet-eval' ({SuiteId})", seedSuite.Id);
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to seed default eval suite — smoke tests may need manual setup");
    }
    var bus = sp.GetRequiredService<IEventBus>();
    Log.Information("Event bus initialized: {EventBusType}", bus.GetType().Name);
}

// ============================================================
// JSON-RPC Handler — string based (used by both HTTP and stdio)
// ============================================================
static async Task<string?> HandleJsonRpcString(string body, Channel<string>? sseChannel, IServiceProvider services)
{
    try
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var jsonrpc = root.TryGetProperty("jsonrpc", out var jr) ? jr.GetString() : "";
        if (jsonrpc != "2.0")
        {
            return MakeRpcError(null, -32600, "Invalid Request: jsonrpc must be 2.0");
        }

        var id = root.TryGetProperty("id", out var idEl) ? idEl : default;
        var method = root.TryGetProperty("method", out var m) ? m.GetString() : "";
        var @params = root.TryGetProperty("params", out var p) ? p : default;

        switch (method)
        {
            case "initialize":
            {
                var caps = new JsonObject { ["tools"] = new JsonObject() };
                var info = new JsonObject { ["name"] = "agent-harness", ["version"] = "1.0.0" };
                return MakeRpcResult(id, new JsonObject
                {
                    ["protocolVersion"] = "2024-11-05",
                    ["capabilities"] = caps,
                    ["serverInfo"] = info
                });
            }

            case "notifications/initialized":
                return null; // No response for notifications

            case "ping":
                return MakeRpcResult(id, new JsonObject { ["status"] = "ok" });

            case "tools/list":
            {
                var tools = BuildToolList();
                return MakeRpcResult(id, new JsonObject { ["tools"] = tools });
            }

            case "tools/call":
            {
                var toolName = @params.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : "";
                var arguments = @params.TryGetProperty("arguments", out var argsEl)
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(argsEl.GetRawText()) ?? new()
                    : new Dictionary<string, object>();

                using var scope = services.CreateScope();
                var sp = scope.ServiceProvider;
                string toolResult;

                switch (toolName)
                {
                    case "start-pipeline":
                    {
                        var t = sp.GetRequiredService<StartPipelineTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "get-status":
                    {
                        var t = sp.GetRequiredService<GetStatusTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "dispatch-agent":
                    {
                        var t = sp.GetRequiredService<DispatchAgentTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "cancel-pipeline":
                    {
                        var t = sp.GetRequiredService<CancelPipelineTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "get-pending-tasks":
                    {
                        var t = sp.GetRequiredService<GetPendingTasksTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "complete-task":
                    {
                        var t = sp.GetRequiredService<CompleteTaskTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "get-pipeline-status":
                    {
                        var t = sp.GetRequiredService<GetPipelineStatusTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "get-pipeline-timeline":
                    {
                        var t = sp.GetRequiredService<TimelineTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "save-artifact":
                    {
                        var t = sp.GetRequiredService<SaveArtifactTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "get-artifact":
                    {
                        var t = sp.GetRequiredService<GetArtifactTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "request-approval":
                    {
                        var t = sp.GetRequiredService<RequestApprovalTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "approve-action":
                    {
                        var t = sp.GetRequiredService<ApproveActionTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "reject-action":
                    {
                        var t = sp.GetRequiredService<RejectActionTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "list-pending-approvals":
                    {
                        var t = sp.GetRequiredService<ListPendingApprovalsTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "record-instinct":
                    {
                        var t = sp.GetRequiredService<RecordInstinctTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "query-instincts":
                    {
                        var t = sp.GetRequiredService<QueryInstinctsTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "get-agent-profile":
                    {
                        var t = sp.GetRequiredService<GetAgentProfileTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "evaluate-agent":
                    {
                        var t = sp.GetRequiredService<EvaluateAgentTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "compare-models":
                    {
                        var t = sp.GetRequiredService<CompareModelsTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    case "route-llm":
                    {
                        var t = sp.GetRequiredService<RouteLlmTool>();
                        toolResult = await t.ExecuteAsync(arguments);
                        break;
                    }
                    default:
                        return MakeRpcError(id, -32601, $"Tool not found: {toolName}");
                }

                return MakeRpcResult(id, new JsonObject
                {
                    ["content"] = new JsonArray
                    {
                        new JsonObject { ["type"] = "text", ["text"] = toolResult }
                    }
                });
            }

            default:
                return MakeRpcError(id, -32601, $"Method not found: {method}");
        }
    }
    catch (JsonException ex)
    {
        Log.Error(ex, "MCP JSON parse error");
        return MakeRpcError(null, -32700, $"Parse error: {ex.Message}");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "MCP handler error");
        return MakeRpcError(null, -32603, $"Internal error: {ex.Message}");
    }
}

// ============================================================
// JSON-RPC Helpers
// ============================================================
static string MakeRpcResult(JsonElement? id, JsonNode result)
{
    var obj = new JsonObject { ["jsonrpc"] = "2.0" };
    if (id.HasValue) obj["id"] = JsonSerializer.SerializeToNode(id.Value);
    obj["result"] = result;
    return obj.ToJsonString();
}

static string MakeRpcError(JsonElement? id, int code, string message)
{
    var obj = new JsonObject { ["jsonrpc"] = "2.0" };
    if (id.HasValue) obj["id"] = JsonSerializer.SerializeToNode(id.Value);
    obj["error"] = new JsonObject { ["code"] = code, ["message"] = message };
    return obj.ToJsonString();
}

static JsonArray BuildToolList()
{
    JsonObject MakeProp(string t, string d) => new() { ["type"] = t, ["description"] = d };

    return new JsonArray
    {
        new JsonObject
        {
            ["name"] = "start-pipeline",
            ["description"] = "Start a new pipeline run with a specified workflow",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["workflow_id"] = MakeProp("string", "The workflow identifier to execute"),
                    ["triggered_by"] = MakeProp("string", "Who/what triggered this pipeline"),
                    ["params"] = MakeProp("object", "Additional parameters for the workflow")
                },
                ["required"] = new JsonArray("workflow_id")
            }
        },
        new JsonObject
        {
            ["name"] = "get-status",
            ["description"] = "Get the current status of a pipeline run",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["pipeline_run_id"] = MakeProp("string", "The pipeline run ID (GUID)")
                },
                ["required"] = new JsonArray("pipeline_run_id")
            }
        },
        new JsonObject
        {
            ["name"] = "dispatch-agent",
            ["description"] = "Dispatch an agent task within a pipeline run",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["pipeline_run_id"] = MakeProp("string", "The pipeline run ID (GUID)"),
                    ["agent_name"] = MakeProp("string", "Name of the agent to dispatch"),
                    ["task_description"] = MakeProp("string", "Description of the task"),
                    ["max_retries"] = MakeProp("number", "Maximum retry attempts"),
                    ["timeout"] = MakeProp("number", "Timeout in seconds")
                },
                ["required"] = new JsonArray("pipeline_run_id", "agent_name", "task_description")
            }
        },
        new JsonObject
        {
            ["name"] = "cancel-pipeline",
            ["description"] = "Cancel a running pipeline",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["pipeline_run_id"] = MakeProp("string", "The pipeline run ID (GUID)"),
                    ["reason"] = MakeProp("string", "Reason for cancellation")
                },
                ["required"] = new JsonArray("pipeline_run_id")
            }
        },
        new JsonObject
        {
            ["name"] = "get-pending-tasks",
            ["description"] = "Get all agent runs currently in Running status across pipelines. External agents poll this to find work.",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["pipeline_run_id"] = MakeProp("string", "Optional: filter by pipeline run ID (GUID)")
                },
                ["required"] = new JsonArray()
            }
        },
        new JsonObject
        {
            ["name"] = "complete-task",
            ["description"] = "Mark an agent run as completed or failed. Called by external agents after finishing work.",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["agent_run_id"] = MakeProp("string", "The agent run ID (GUID)"),
                    ["status"] = MakeProp("string", "'completed' or 'failed'"),
                    ["confidence"] = MakeProp("number", "Confidence score 0.0-1.0 (default 0.95)"),
                    ["artifact_ref"] = MakeProp("string", "Reference to output artifact"),
                    ["error_message"] = MakeProp("string", "Error message if status=failed")
                },
                ["required"] = new JsonArray("agent_run_id", "status")
            }
        },
        new JsonObject
        {
            ["name"] = "get-pipeline-status",
            ["description"] = "Enhanced pipeline status with all agent runs and quality gates",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["pipeline_run_id"] = MakeProp("string", "The pipeline run ID (GUID)")
                },
                ["required"] = new JsonArray("pipeline_run_id")
            }
        },
        new JsonObject
        {
            ["name"] = "get-pipeline-timeline",
            ["description"] = "Get execution timeline for a pipeline run with ordered events and durations",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["pipeline_run_id"] = MakeProp("string", "The pipeline run ID (GUID)")
                },
                ["required"] = new JsonArray("pipeline_run_id")
            }
        },
        new JsonObject
        {
            ["name"] = "save-artifact",
            ["description"] = "Save an artifact (test results, logs, build output) associated with a pipeline run",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["pipeline_run_id"] = MakeProp("string", "The pipeline run ID (GUID)"),
                    ["name"] = MakeProp("string", "Artifact name"),
                    ["content_type"] = MakeProp("string", "MIME type (default: text/plain)"),
                    ["content_base64"] = MakeProp("string", "Base64-encoded artifact content"),
                    ["storage_path"] = MakeProp("string", "Storage path reference (optional)")
                },
                ["required"] = new JsonArray("pipeline_run_id", "name")
            }
        },
        new JsonObject
        {
            ["name"] = "get-artifact",
            ["description"] = "Retrieve an artifact by ID with its content",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["artifact_id"] = MakeProp("string", "The artifact ID (GUID)")
                },
                ["required"] = new JsonArray("artifact_id")
            }
        },
        new JsonObject
        {
            ["name"] = "request-approval",
            ["description"] = "Request human approval for a guarded action",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["action_type"] = MakeProp("string", "Action type to validate"),
                    ["requested_by"] = MakeProp("string", "Requester identity"),
                    ["details"] = MakeProp("string", "Action details for review")
                },
                ["required"] = new JsonArray("action_type")
            }
        },
        new JsonObject
        {
            ["name"] = "approve-action",
            ["description"] = "Approve a pending human-in-the-loop action",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["pending_approval_id"] = MakeProp("string", "Pending approval ID (GUID)"),
                    ["approved_by"] = MakeProp("string", "Human approver identity")
                },
                ["required"] = new JsonArray("pending_approval_id")
            }
        },
        new JsonObject
        {
            ["name"] = "reject-action",
            ["description"] = "Reject a pending human-in-the-loop action",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["pending_approval_id"] = MakeProp("string", "Pending approval ID (GUID)"),
                    ["rejected_by"] = MakeProp("string", "Human reviewer identity"),
                    ["reason"] = MakeProp("string", "Rejection reason")
                },
                ["required"] = new JsonArray("pending_approval_id")
            }
        },
        new JsonObject
        {
            ["name"] = "list-pending-approvals",
            ["description"] = "List pending human-in-the-loop approvals",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject(),
                ["required"] = new JsonArray()
            }
        },
        new JsonObject
        {
            ["name"] = "record-instinct",
            ["description"] = "Save a learned instinct after a successful fix. Agents call this to share knowledge across sessions.",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["agent_name"] = MakeProp("string", "Which agent learned this (e.g. dotnet, angular, qa)"),
                    ["error_pattern"] = MakeProp("string", "The error or issue that was encountered"),
                    ["error_category"] = MakeProp("string", "Category: build | runtime | test | security | config | migration | other"),
                    ["fix_description"] = MakeProp("string", "How the issue was resolved"),
                    ["fix_artifact_ref"] = MakeProp("string", "Optional reference to the fix (commit hash, file path, PR URL)"),
                    ["confidence"] = MakeProp("number", "Confidence score 0.0-1.0 (default 0.85)")
                },
                ["required"] = new JsonArray("agent_name", "error_pattern", "error_category", "fix_description")
            }
        },
        new JsonObject
        {
            ["name"] = "query-instincts",
            ["description"] = "Search for past instincts similar to the current issue. Returns matches ranked by confidence.",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["error_pattern"] = MakeProp("string", "The error or issue description to match"),
                    ["agent_name"] = MakeProp("string", "Optional: filter by agent type"),
                    ["min_confidence"] = MakeProp("number", "Minimum similarity threshold 0.0-1.0 (default 0.3)")
                },
                ["required"] = new JsonArray("error_pattern")
            }
        },
        new JsonObject
        {
            ["name"] = "get-agent-profile",
            ["description"] = "Get the Agent Intelligence Score (AIS) and performance profile for a specific agent.",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["agent_name"] = MakeProp("string", "Name of the agent (e.g. dotnet, angular, qa)")
                },
                ["required"] = new JsonArray("agent_name")
            }
        },
        new JsonObject
        {
            ["name"] = "evaluate-agent",
            ["description"] = "Run an eval suite against an agent/model and return pass@1, pass@k, and judge score.",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["suite_name"] = MakeProp("string", "Name of the eval suite to run"),
                    ["target_agent"] = MakeProp("string", "Agent name to evaluate"),
                    ["target_model"] = MakeProp("string", "Optional model name (e.g. gpt-4, claude-3)"),
                    ["k"] = MakeProp("number", "Number of attempts per task (default: 5)")
                },
                ["required"] = new JsonArray("suite_name", "target_agent")
            }
        },
        new JsonObject
        {
            ["name"] = "compare-models",
            ["description"] = "Compare multiple models on the same eval suite and return a ranking with winner.",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["suite_name"] = MakeProp("string", "Name of the eval suite to run"),
                    ["target_agent"] = MakeProp("string", "Agent name to evaluate"),
                    ["models"] = MakeProp("array", "Array of model names to compare (e.g. [\"gpt-4\",\"claude-3\"])"),
                    ["k"] = MakeProp("number", "Number of attempts per task (default: 5)")
                },
                ["required"] = new JsonArray("suite_name", "target_agent", "models")
            }
        },
        new JsonObject
        {
            ["name"] = "route-llm",
            ["description"] = "Route an LLM task to the most cost-effective model based on capability requirements and budget.",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["task_description"] = MakeProp("string", "Description of the task to route"),
                    ["task_category"] = MakeProp("string", "Task complexity category: simple | moderate | moderate+ | complex | security_sensitive (default: moderate)"),
                    ["agent_name"] = MakeProp("string", "Name of the agent making the request"),
                    ["redact_pii"] = MakeProp("boolean", "Whether to redact PII from the task description"),
                    ["available_models"] = MakeProp("array", "Optional: restrict to specific models (e.g. [\"gpt-4\",\"claude-3\"])")
                },
                ["required"] = new JsonArray("task_description")
            }
        }
    };
}
