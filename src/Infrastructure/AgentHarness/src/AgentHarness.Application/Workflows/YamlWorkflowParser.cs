using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace His.Hope.AgentHarness.Application.Workflows;

/// <summary>
/// Parses YAML workflow definition strings into <see cref="YamlWorkflowDefinition"/> objects.
/// Uses YamlDotNet for structured deserialization with a fallback line-by-line parser
/// for simple field extraction.
/// </summary>
public class YamlWorkflowParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Parses the given YAML content into a <see cref="YamlWorkflowDefinition"/>.
    /// Uses YamlDotNet for full deserialization; falls back to line-by-line parsing
    /// for top-level fields when the full deserializer encounters issues.
    /// </summary>
    public YamlWorkflowDefinition Parse(string yamlContent)
    {
        // Attempt full YamlDotNet deserialization first
        try
        {
            var result = Deserializer.Deserialize<YamlWorkflowDefinition>(yamlContent);
            if (result != null && !string.IsNullOrWhiteSpace(result.Name))
                return result;
        }
        catch
        {
            // Fall through to manual parsing
        }

        // Fallback: manual line-by-line parsing
        return ManualParse(yamlContent);
    }

    /// <summary>
    /// Manual line-by-line parsing for top-level fields.
    /// Full YAML parser would parse the complete structure including phases and parallel agents.
    /// </summary>
    private static YamlWorkflowDefinition ManualParse(string yamlContent)
    {
        var def = new YamlWorkflowDefinition();
        var lines = yamlContent.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("name:"))
                def.Name = ExtractValue(trimmed);
            if (trimmed.StartsWith("description:"))
                def.Description = ExtractValue(trimmed);
            if (trimmed.StartsWith("version:"))
                def.Version = ExtractValue(trimmed);
            // Note: full structure parsing (triggers, phases, etc.)
            // would require a full YAML parser to handle nested elements
        }

        return def;
    }

    private static string ExtractValue(string line)
    {
        var colon = line.IndexOf(':');
        return colon >= 0 ? line[(colon + 1)..].Trim().Trim('"') : string.Empty;
    }
}
