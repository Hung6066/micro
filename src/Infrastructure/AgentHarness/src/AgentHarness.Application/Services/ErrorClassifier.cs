namespace His.Hope.AgentHarness.Application.Services;

public enum ErrorCategory
{
    CompilationError,
    TestFailure,
    ContractViolation,
    QualityGateFailure,
    InfrastructureError,
    KnownGotcha,
    LogicError,
    Unknown
}

public class ErrorClassifier
{
    private static readonly Dictionary<string, ErrorCategory> PatternMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["error CS"] = ErrorCategory.CompilationError,
        ["error BC"] = ErrorCategory.CompilationError,
        ["FAILED"] = ErrorCategory.TestFailure,
        ["test failed"] = ErrorCategory.TestFailure,
        ["contract violation"] = ErrorCategory.ContractViolation,
        ["buf breaking"] = ErrorCategory.ContractViolation,
        ["schema mismatch"] = ErrorCategory.ContractViolation,
        ["connection refused"] = ErrorCategory.InfrastructureError,
        ["timeout"] = ErrorCategory.InfrastructureError,
        ["hardcoded secret"] = ErrorCategory.KnownGotcha,
        ["permissionguard"] = ErrorCategory.KnownGotcha,
        ["deadlock"] = ErrorCategory.KnownGotcha,
    };

    public ErrorCategory Classify(string errorOutput)
    {
        if (string.IsNullOrWhiteSpace(errorOutput)) return ErrorCategory.Unknown;
        foreach (var (pattern, category) in PatternMap)
        {
            if (errorOutput.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return category;
        }
        return ErrorCategory.Unknown;
    }

    public bool IsAutoFixable(ErrorCategory category) => category switch
    {
        ErrorCategory.CompilationError => true,
        ErrorCategory.TestFailure => true,
        ErrorCategory.ContractViolation => true,
        ErrorCategory.KnownGotcha => true,
        ErrorCategory.QualityGateFailure => true,
        ErrorCategory.InfrastructureError => false,
        ErrorCategory.LogicError => false,
        ErrorCategory.Unknown => false,
        _ => false
    };
}
