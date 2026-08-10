namespace His.Hope.AgentHarness.Mcp;

public class McpServerConfig
{
    public string Name { get; set; } = "agent-harness";
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = "His.Hope Agent Harness — Runtime pipeline orchestration for OpenCode agents";
    public int Port { get; set; } = 5200;
    public string DatabaseConnectionString { get; set; } = "Host=localhost;Port=5433;Database=harness;Username=harness;Password=harness";
    public string RabbitMQConnectionString { get; set; } = "amqp://admin:admin@localhost:5672/";
    public bool UseTemporal { get; set; }
    public string TemporalServerUrl { get; set; } = "localhost:7233";
    public int MaxPipelineQueue { get; set; } = 10;
    public int MaxAgentQueue { get; set; } = 20;
    public int CircuitBreakerThreshold { get; set; } = 5;
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
    public int LoopEngineerMaxIterations { get; set; } = 3;
    public decimal LoopEngineerConfidenceThreshold { get; set; } = 0.8m;
}
