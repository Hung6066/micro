namespace His.Hope.AgentHarness.Application.DTOs;

public record ModelComparisonDto
{
    public string EvalSuiteName { get; init; } = string.Empty;
    public string TargetAgent { get; init; } = string.Empty;
    public IReadOnlyList<EvalRunDto> Results { get; init; } = Array.Empty<EvalRunDto>();
    public string WinnerModel { get; init; } = string.Empty;
}
