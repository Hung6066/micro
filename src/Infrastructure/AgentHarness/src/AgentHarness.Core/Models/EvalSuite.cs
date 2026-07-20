namespace His.Hope.AgentHarness.Core.Models;

public class EvalSuite
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Domain { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string DefinitionJson { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private EvalSuite() { }

    public static EvalSuite Create(string name, string domain, string description, string definitionJson)
    {
        return new EvalSuite
        {
            Id = Guid.NewGuid(),
            Name = name,
            Domain = domain,
            Description = description,
            DefinitionJson = definitionJson,
            CreatedAt = DateTime.UtcNow
        };
    }
}
