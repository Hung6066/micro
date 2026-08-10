using System.Text.RegularExpressions;

namespace His.Hope.AgentHarness.Application.Services;

public partial class PromptTemplateService
{
    private readonly Dictionary<string, string> _templates;

    public PromptTemplateService()
    {
        _templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["default-agent"] = """
You are an autonomous agent in the His.Hope hospital information system.
Your task: {{task}}
You have access to: {{tools}}
Respond with clear, actionable output.
""",
            ["code-reviewer"] = """
You are a senior code reviewer for His.Hope.
Review the following code changes for:
- Correctness
- Clean Architecture compliance
- Security (HIPAA, PHI exposure)
- Test coverage
{{changes}}
""",
            ["tester"] = """
You are a QA engineer for His.Hope.
Write tests for: {{feature}}
Coverage targets: {{coverage}}
Use the existing test patterns from the project.
""",
        };
    }

    public PromptTemplateService(Dictionary<string, string> templates)
    {
        _templates = new Dictionary<string, string>(templates, StringComparer.OrdinalIgnoreCase);
    }

    public string GetPrompt(string key, Dictionary<string, string>? vars = null)
    {
        if (!_templates.TryGetValue(key, out var template))
            throw new KeyNotFoundException($"Prompt template '{key}' not found. Available: {string.Join(", ", _templates.Keys)}");

        if (vars == null || vars.Count == 0)
            return template;

        return TemplateVarRegex().Replace(template, match =>
        {
            var varName = match.Groups[1].Value;
            return vars.TryGetValue(varName, out var value) ? value : match.Value;
        });
    }

    public void RegisterTemplate(string key, string template)
    {
        _templates[key] = template;
    }

    public IReadOnlyDictionary<string, string> GetAllTemplates() => _templates.AsReadOnly();

    [GeneratedRegex(@"\{\{(\w+)\}\}", RegexOptions.Compiled)]
    private static partial Regex TemplateVarRegex();
}
