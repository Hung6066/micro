namespace His.Hope.ManufacturingService.Domain;

public sealed record QualityTestResultInput(string TestCode, string TestName, decimal MeasuredValue, string Uom, string Result, decimal? LowerLimit, decimal? UpperLimit, string? Method = null, string? EvidenceReference = null);

public static class QualityInspectionPolicy
{
    private static readonly string[] ResultStatuses = ["Pass", "Fail", "NotApplicable"];

    public static string? Validate(IReadOnlyList<QualityTestResultInput>? results, string inspectionStatus)
    {
        if (results is null || results.Count == 0) return null;
        if (results.Count > 50) return "too_many_quality_test_results";

        foreach (var result in results)
        {
            if (string.IsNullOrWhiteSpace(result.TestCode) || result.TestCode.Trim().Length > 100 ||
                string.IsNullOrWhiteSpace(result.TestName) || result.TestName.Trim().Length > 200 ||
                string.IsNullOrWhiteSpace(result.Uom) || result.Uom.Trim().Length > 30 ||
                !ResultStatuses.Contains(result.Result, StringComparer.OrdinalIgnoreCase))
                return "invalid_quality_test_result";
            if (result.LowerLimit.HasValue && result.UpperLimit.HasValue && result.LowerLimit > result.UpperLimit)
                return "invalid_quality_test_limit";
            var controlCode = result.TestCode.Trim();
            if ((controlCode.StartsWith("CCP-", StringComparison.OrdinalIgnoreCase) || controlCode.StartsWith("OPRP-", StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(result.Method) || string.IsNullOrWhiteSpace(result.EvidenceReference)))
                return "control_measurement_evidence_required";
        }

        if (results.Any(x => x.Result.Equals("Fail", StringComparison.OrdinalIgnoreCase)) &&
            !inspectionStatus.Equals("Fail", StringComparison.OrdinalIgnoreCase))
            return "quality_test_failure_requires_failed_inspection";
        if (inspectionStatus.Equals("Pass", StringComparison.OrdinalIgnoreCase) &&
            results.Any(x => !x.Result.Equals("Pass", StringComparison.OrdinalIgnoreCase) && !x.Result.Equals("NotApplicable", StringComparison.OrdinalIgnoreCase)))
            return "quality_test_results_do_not_support_pass";
        return null;
    }
}
