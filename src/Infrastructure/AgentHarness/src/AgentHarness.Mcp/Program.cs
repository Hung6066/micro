using Serilog;
using MediatR;
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
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    var config = builder.Configuration
        .GetSection("AgentHarness")
        .Get<McpServerConfig>() ?? new McpServerConfig();

    // Register persistence, event bus, and agent dispatcher
    builder.Services.AddHarnessPersistence(config.DatabaseConnectionString);
    builder.Services.AddSingleton<IEventBus>(_ => new RabbitMQEventBus(config.RabbitMQHost));
    builder.Services.AddScoped<IAgentDispatcher, OpenCodeAgentDispatcher>();

    // Register MediatR with handlers and pipeline behaviors
    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(StartPipelineHandler).Assembly);
        cfg.AddOpenBehavior(typeof(CircuitBreakerBehavior<,>));
        cfg.AddOpenBehavior(typeof(RetryBehavior<,>));
        cfg.AddOpenBehavior(typeof(TimeoutBehavior<,>));
    });

    // Register pipeline engine and supporting services
    builder.Services.AddScoped<BackpressureController>();
    builder.Services.AddScoped<AgentPoolManager>();
    builder.Services.AddScoped<ErrorClassifier>();
    builder.Services.AddScoped<ConfidenceScorer>();
    builder.Services.AddScoped<ILoopEngineer, LoopEngineer>();
    builder.Services.AddScoped<IPipelineEngine, PipelineEngine>();

    // Register MCP tools as singletons
    builder.Services.AddSingleton<StartPipelineTool>();
    builder.Services.AddSingleton<GetStatusTool>();
    builder.Services.AddSingleton<DispatchAgentTool>();
    builder.Services.AddSingleton<CancelPipelineTool>();

    var app = builder.Build();

    // Auto-create database schema on startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<HarnessDbContext>();
        db.Database.EnsureCreated();
    }

    // Health endpoint
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "agent-harness" }));

    // MCP tool endpoints
    app.MapPost("/mcp/start-pipeline", async (StartPipelineTool tool, HttpContext ctx) =>
    {
        var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
        return Results.Ok(await tool.ExecuteAsync(p));
    });

    app.MapPost("/mcp/get-status", async (GetStatusTool tool, HttpContext ctx) =>
    {
        var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
        return Results.Ok(await tool.ExecuteAsync(p));
    });

    app.MapPost("/mcp/dispatch-agent", async (DispatchAgentTool tool, HttpContext ctx) =>
    {
        var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
        return Results.Ok(await tool.ExecuteAsync(p));
    });

    app.MapPost("/mcp/cancel-pipeline", async (CancelPipelineTool tool, HttpContext ctx) =>
    {
        var p = await ctx.Request.ReadFromJsonAsync<Dictionary<string, object>>() ?? new();
        return Results.Ok(await tool.ExecuteAsync(p));
    });

    Log.Information("Agent Harness MCP Server starting on port {Port}", config.Port);
    app.Run($"http://0.0.0.0:{config.Port}");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Agent Harness MCP Server terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
