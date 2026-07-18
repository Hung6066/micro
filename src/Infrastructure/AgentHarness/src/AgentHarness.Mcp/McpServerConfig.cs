namespace His.Hope.AgentHarness.Mcp;

public class McpServerConfig
{
    public string Name { get; set; } = "agent-harness";
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = "His.Hope Agent Harness — Runtime pipeline orchestration for OpenCode agents";
    public int Port { get; set; } = 5200;
    public string DatabaseConnectionString { get; set; } = "Host=localhost;Port=5433;Database=harness;Username=harness;Password=harness";
    public string RabbitMQConnectionString { get; set; } = "amqp://admin:admin@localhost:5672/";
}
