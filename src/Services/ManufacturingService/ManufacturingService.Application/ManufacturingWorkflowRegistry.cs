namespace His.Hope.ManufacturingService.Application;

public sealed record WorkflowStepDefinition(string Key, string I18nGroup);

public sealed record ManufacturingWorkflowDefinition(
    string EntityType,
    IReadOnlyList<WorkflowStepDefinition> Steps,
    IReadOnlyDictionary<string, string>? StatusAliases = null,
    IReadOnlyList<string>? TerminalStatuses = null);

public static class ManufacturingWorkflowRegistry
{
    private static readonly IReadOnlyDictionary<string, ManufacturingWorkflowDefinition> Definitions =
        new Dictionary<string, ManufacturingWorkflowDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["production-order"] = new(
                "production-order",
                [
                    new("Draft", "productionOrderStatus"),
                    new("Planned", "productionOrderStatus"),
                    new("Released", "productionOrderStatus"),
                    new("InProgress", "productionOrderStatus"),
                    new("Completed", "productionOrderStatus"),
                ],
                new Dictionary<string, string> { ["Open"] = "InProgress" },
                ["Cancelled"]),
            ["production-batch"] = new(
                "production-batch",
                [
                    new("Created", "productionBatchStatus"),
                    new("Started", "productionBatchStatus"),
                    new("Completed", "productionBatchStatus"),
                ],
                new Dictionary<string, string>
                {
                    ["Paused"] = "Started",
                    ["InProgress"] = "Started",
                    ["AwaitingQA"] = "Started",
                    ["Planned"] = "Created",
                    ["Released"] = "Created",
                },
                ["Cancelled"]),
            ["purchase-order"] = new(
                "purchase-order",
                [
                    new("Draft", "purchaseOrderStatus"),
                    new("Approved", "purchaseOrderStatus"),
                    new("PartiallyReceived", "purchaseOrderStatus"),
                    new("Closed", "purchaseOrderStatus"),
                ],
                new Dictionary<string, string>
                {
                    ["Submitted"] = "Draft",
                    ["Ordered"] = "Approved",
                    ["Received"] = "PartiallyReceived",
                },
                ["Cancelled"]),
            ["deviation"] = new(
                "deviation",
                [
                    new("Requested", "deviationStatus"),
                    new("Approved", "deviationStatus"),
                    new("Closed", "deviationStatus"),
                ],
                TerminalStatuses: ["Rejected"]),
            ["capa"] = new(
                "capa",
                [
                    new("Open", "capaStatus"),
                    new("InProgress", "capaStatus"),
                    new("Verified", "capaStatus"),
                    new("Closed", "capaStatus"),
                ]),
            ["quality-inspection"] = new(
                "quality-inspection",
                [
                    new("Pending", "qualityInspectionStatus"),
                    new("Pass", "qualityInspectionStatus"),
                ],
                TerminalStatuses: ["Fail", "Rejected"]),
            ["quality-sample"] = new(
                "quality-sample",
                [
                    new("Pending", "qualitySampleDisposition"),
                    new("Released", "qualitySampleDisposition"),
                ],
                new Dictionary<string, string> { ["Accepted"] = "Released" },
                ["Rejected"]),
            ["inspection-plan"] = new(
                "inspection-plan",
                [
                    new("Draft", "governanceLifecycleStatus"),
                    new("Approved", "governanceLifecycleStatus"),
                ],
                new Dictionary<string, string> { ["Submitted"] = "Draft" }),
            ["recipe"] = new(
                "recipe",
                [
                    new("Draft", "governanceLifecycleStatus"),
                    new("Submitted", "governanceLifecycleStatus"),
                    new("Approved", "governanceLifecycleStatus"),
                    new("Retired", "governanceLifecycleStatus"),
                ]),
            ["product-specification"] = new(
                "product-specification",
                [
                    new("Draft", "governanceLifecycleStatus"),
                    new("Approved", "governanceLifecycleStatus"),
                    new("Retired", "governanceLifecycleStatus"),
                ]),
        };

    public static IReadOnlyList<string> EntityTypes => Definitions.Keys.OrderBy(x => x).ToList();

    public static ManufacturingWorkflowDefinition? TryGet(string entityType) =>
        Definitions.TryGetValue(entityType.Trim(), out var definition) ? definition : null;
}
