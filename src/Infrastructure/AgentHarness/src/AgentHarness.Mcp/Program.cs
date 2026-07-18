using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using MediatR;
using Microsoft.Extensions.Configuration;
using Serilog;
using His.Hope.AgentHarness.Mcp;
using His.Hope.AgentHarness.Mcp.Tools;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Infrastructure.Persistence;
using His.Hope.AgentHarness.Infrastructure.Dispatch;
using His.Hope.AgentHarness.Infrastructure.EventBus;
using His.Hope.AgentHarness.Application.Behaviors;
using His.Hope.AgentHarness.Application.Commands.StartPipeline;
using His.Hope.AgentHarness.Application.Services;

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

    var config = builder.Configuration
        .GetSection("AgentHarness")
        .Get<McpServerConfig>() ?? new McpServerConfig();

    ConfigureServices(builder.Services, config);

    var app = builder.Build();

    // Auto-create database schema
    using (var scope = app.Services.CreateScope())
    {
        InitializeDatabase(scope.ServiceProvider);
    }

    // SSE sessions: maps sessionId -> Channel<string> for sending
    // JSON-RPC responses back through the SSE stream
    var sseSessions = new ConcurrentDictionary<string, Channel<string>>();

    // Health endpoint
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "agent-harness" }));

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

    // 3. Legacy REST endpoints (backward compat)
    app.MapPost("/mcp/start-pipeline", async (StartPipelineTool tool, HttpContext ctx) =>
    {
        try
        {
            var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
            return Results.Ok(await tool.ExecuteAsync(p));
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
            return Results.Ok(await tool.ExecuteAsync(p));
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
            return Results.Ok(await tool.ExecuteAsync(p));
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
            return Results.Ok(await tool.ExecuteAsync(p));
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
            return Results.Ok(await tool.ExecuteAsync(p));
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
            return Results.Ok(await tool.ExecuteAsync(p));
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
            return Results.Ok(await tool.ExecuteAsync(p));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetPipelineStatus failed");
            return Results.Json(new { error = ex.Message }, statusCode: 404);
        }
    });

    Log.Information("Agent Harness MCP Server starting on port {Port}", config.Port);
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
        InitializeDatabase(scope.ServiceProvider);
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
    services.AddScoped<IAgentDispatcher, OpenCodeAgentDispatcher>();

    // Register MediatR with handlers and pipeline behaviors
    services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(StartPipelineHandler).Assembly);
        cfg.AddOpenBehavior(typeof(CircuitBreakerBehavior<,>));
        cfg.AddOpenBehavior(typeof(RetryBehavior<,>));
        cfg.AddOpenBehavior(typeof(TimeoutBehavior<,>));
    });

    // Register pipeline engine and supporting services
    services.AddScoped<BackpressureController>();
    services.AddScoped<AgentPoolManager>();
    services.AddScoped<ErrorClassifier>();
    services.AddScoped<ConfidenceScorer>();
    services.AddScoped<ILoopEngineer, LoopEngineer>();
    services.AddScoped<IPipelineEngine, PipelineEngine>();

    // Register MCP tools as scoped
    services.AddScoped<StartPipelineTool>();
    services.AddScoped<GetStatusTool>();
    services.AddScoped<DispatchAgentTool>();
    services.AddScoped<CancelPipelineTool>();
    services.AddScoped<GetPendingTasksTool>();
    services.AddScoped<CompleteTaskTool>();
    services.AddScoped<GetPipelineStatusTool>();
}

static void InitializeDatabase(IServiceProvider sp)
{
    var db = sp.GetRequiredService<HarnessDbContext>();
    db.Database.EnsureCreated();
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
        }
    };
}
