namespace His.Hope.AgentHarness.Core.Models;

public class AgentPoolState
{
    public string AgentName { get; private set; } = string.Empty;
    public int AvailableSlots { get; private set; }
    public int TotalSlots { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime LastHeartbeat { get; private set; }
    public CircuitState CircuitState { get; private set; } = CircuitState.Closed;

    private AgentPoolState() { }

    public static AgentPoolState Create(string agentName, int totalSlots)
    {
        return new AgentPoolState
        {
            AgentName = agentName,
            AvailableSlots = totalSlots,
            TotalSlots = totalSlots,
            IsEnabled = true,
            LastHeartbeat = DateTime.UtcNow
        };
    }

    public void UpdateHeartbeat() => LastHeartbeat = DateTime.UtcNow;
    public void Toggle(bool enabled) => IsEnabled = enabled;
    public void UpdateCircuitState(CircuitState state) => CircuitState = state;
}
