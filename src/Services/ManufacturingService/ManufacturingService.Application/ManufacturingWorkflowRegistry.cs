using His.Hope.ManufacturingService.Domain;

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
                    new(ManufacturingStatusCodes.Draft, "productionOrderStatus"),
                    new("Planned", "productionOrderStatus"),
                    new(ManufacturingStatusCodes.Released, "productionOrderStatus"),
                    new("InProgress", "productionOrderStatus"),
                    new(ManufacturingStatusCodes.Completed, "productionOrderStatus"),
                ],
                new Dictionary<string, string> { ["Open"] = "InProgress" },
                [ManufacturingStatusCodes.Cancelled]),
            ["production-batch"] = new(
                "production-batch",
                [
                    new(ManufacturingStatusCodes.Created, "productionBatchStatus"),
                    new(ManufacturingStatusCodes.Started, "productionBatchStatus"),
                    new(ManufacturingStatusCodes.Completed, "productionBatchStatus"),
                ],
                new Dictionary<string, string>
                {
                    ["Paused"] = ManufacturingStatusCodes.Started,
                    ["InProgress"] = ManufacturingStatusCodes.Started,
                    ["AwaitingQA"] = ManufacturingStatusCodes.Started,
                    ["Planned"] = ManufacturingStatusCodes.Created,
                    [ManufacturingStatusCodes.Released] = ManufacturingStatusCodes.Created,
                },
                [ManufacturingStatusCodes.Cancelled]),
            ["purchase-order"] = new(
                "purchase-order",
                [
                    new(ManufacturingStatusCodes.Draft, "purchaseOrderStatus"),
                    new(ManufacturingStatusCodes.Approved, "purchaseOrderStatus"),
                    new(ManufacturingStatusCodes.PartiallyReceived, "purchaseOrderStatus"),
                    new(ManufacturingStatusCodes.Closed, "purchaseOrderStatus"),
                ],
                new Dictionary<string, string>
                {
                    [ManufacturingStatusCodes.Submitted] = ManufacturingStatusCodes.Draft,
                    ["Ordered"] = ManufacturingStatusCodes.Approved,
                    ["Received"] = ManufacturingStatusCodes.PartiallyReceived,
                },
                [ManufacturingStatusCodes.Cancelled]),
            ["deviation"] = new(
                "deviation",
                [
                    new("Requested", "deviationStatus"),
                    new(ManufacturingStatusCodes.Approved, "deviationStatus"),
                    new(ManufacturingStatusCodes.Closed, "deviationStatus"),
                ],
                TerminalStatuses: [ManufacturingStatusCodes.Rejected]),
            ["capa"] = new(
                "capa",
                [
                    new("Open", "capaStatus"),
                    new("InProgress", "capaStatus"),
                    new("Verified", "capaStatus"),
                    new(ManufacturingStatusCodes.Closed, "capaStatus"),
                ]),
            ["quality-inspection"] = new(
                "quality-inspection",
                [
                    new(ManufacturingStatusCodes.Pending, "qualityInspectionStatus"),
                    new("Pass", "qualityInspectionStatus"),
                ],
                TerminalStatuses: ["Fail", ManufacturingStatusCodes.Rejected]),
            ["quality-sample"] = new(
                "quality-sample",
                [
                    new(ManufacturingStatusCodes.Pending, "qualitySampleDisposition"),
                    new(ManufacturingStatusCodes.Released, "qualitySampleDisposition"),
                ],
                new Dictionary<string, string> { ["Accepted"] = ManufacturingStatusCodes.Released },
                [ManufacturingStatusCodes.Rejected]),
            ["inspection-plan"] = new(
                "inspection-plan",
                [
                    new(ManufacturingStatusCodes.Draft, "governanceLifecycleStatus"),
                    new(ManufacturingStatusCodes.Approved, "governanceLifecycleStatus"),
                ],
                new Dictionary<string, string> { [ManufacturingStatusCodes.Submitted] = ManufacturingStatusCodes.Draft }),
            ["recipe"] = new(
                "recipe",
                [
                    new(ManufacturingStatusCodes.Draft, "governanceLifecycleStatus"),
                    new(ManufacturingStatusCodes.Submitted, "governanceLifecycleStatus"),
                    new(ManufacturingStatusCodes.Approved, "governanceLifecycleStatus"),
                    new("Retired", "governanceLifecycleStatus"),
                ]),
            ["product-specification"] = new(
                "product-specification",
                [
                    new(ManufacturingStatusCodes.Draft, "governanceLifecycleStatus"),
                    new(ManufacturingStatusCodes.Approved, "governanceLifecycleStatus"),
                    new("Retired", "governanceLifecycleStatus"),
                ]),
        };

    public static IReadOnlyList<string> EntityTypes => Definitions.Keys.OrderBy(x => x).ToList();

    public static ManufacturingWorkflowDefinition? TryGet(string entityType) =>
        Definitions.TryGetValue(entityType.Trim(), out var definition) ? definition : null;
}
