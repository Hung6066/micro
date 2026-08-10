using Serilog;
using Temporalio.Extensions.Hosting;
using His.Hope.AgentHarness.Infrastructure.Persistence;
using His.Hope.AgentHarness.Infrastructure.Temporal;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateSlimBuilder(args);
    builder.Host.UseSerilog();

    var temporalServer = builder.Configuration.GetValue<string>("Temporal__ServerUrl") ?? "localhost:7233";
    var dbConnection = builder.Configuration.GetValue<string>("AgentHarness__DatabaseConnectionString")
        ?? "Host=localhost;Port=5433;Database=harness;Username=harness;Password=harness";

    builder.Services.AddHarnessPersistence(dbConnection);

    builder.Services.AddTemporalClient(options =>
    {
        options.TargetHost = temporalServer;
        options.Namespace = "default";
    });

    builder.Services.AddHostedTemporalWorker("default", "agent-harness")
        .AddScopedActivities<AgentActivities>()
        .AddWorkflow<PipelineWorkflow>();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<HarnessDbContext>();
        db.Database.EnsureCreated();
    }

    app.MapGet("/health", () => Results.Ok(new
    {
        status = "healthy",
        service = "temporal-worker",
        temporal = temporalServer
    }));

    Log.Information("Temporal Worker starting — connecting to {Server}", temporalServer);

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Temporal Worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
