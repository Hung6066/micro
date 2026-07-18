namespace His.Hope.AgentHarness.Core.Models;

public class AgentDefinition
{
    public string Name { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public List<string> Capabilities { get; private set; } = new();
    public bool IsAvailable { get; private set; } = true;

    private AgentDefinition() { }

    public static AgentDefinition Create(string name, string displayName, string description)
    {
        return new AgentDefinition
        {
            Name = name,
            DisplayName = displayName,
            Description = description,
            Capabilities = new()
        };
    }

    public void AddCapability(string capability) => Capabilities.Add(capability);
    public void SetAvailability(bool available) => IsAvailable = available;
}
