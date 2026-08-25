namespace His.Hope.ManufacturingService.Application;

public sealed record ProductionOrderValidationInput(string OrderNumber, string ProductSku, Guid RecipeId, decimal TargetQuantity, string OutputUom);
public sealed record ProductionBatchValidationInput(string OrderStatus, string BatchNumber, decimal PlannedQuantity);
public sealed record OperationMeasurementValidationInput(int Sequence, string ProcessStep, string Operator, decimal InputQuantity, decimal OutputQuantity, string QcStatus);
public sealed record BatchTransitionValidationInput(string CurrentStatus, string TargetStatus, bool HasOperations, bool RequiredOperationsComplete, bool RequiredQualityPassed, bool MachineAvailable);

public static class ProductionPolicy
{
    public static string? ValidateOrder(ProductionOrderValidationInput input) =>
        input.TargetQuantity <= 0 || string.IsNullOrWhiteSpace(input.OrderNumber) || string.IsNullOrWhiteSpace(input.ProductSku) ||
        input.RecipeId == Guid.Empty || string.IsNullOrWhiteSpace(input.OutputUom) ? "invalid_production_order" : null;

    public static string? ValidateBatch(ProductionBatchValidationInput input)
    {
        if (string.IsNullOrWhiteSpace(input.BatchNumber) || input.PlannedQuantity <= 0) return "invalid_production_batch";
        return input.OrderStatus == "Released" ? null : "production_order_not_released";
    }

    public static string? ValidateOperation(ProductionBatchValidationInput batch, OperationMeasurementValidationInput input)
    {
        if (input.Sequence < 0 || input.InputQuantity <= 0 || input.OutputQuantity < 0 || input.OutputQuantity > input.InputQuantity ||
            string.IsNullOrWhiteSpace(input.ProcessStep) || string.IsNullOrWhiteSpace(input.Operator) || string.IsNullOrWhiteSpace(input.QcStatus))
            return "invalid_operation_measurement";
        return batch.OrderStatus is "Started" or "Paused" ? null : "batch_not_started";
    }

    public static string? ValidateTransition(BatchTransitionValidationInput input)
    {
        if (input.TargetStatus == "Started" && !input.MachineAvailable) return "machine_unavailable";
        if ((input.CurrentStatus, input.TargetStatus) is ("Created", "Started") or ("Started", "Paused") or ("Paused", "Started")) return null;
        if ((input.CurrentStatus, input.TargetStatus) is ("Started", "Completed") or ("Paused", "Completed"))
        {
            if (!input.HasOperations) return "required_operation_incomplete";
            if (!input.RequiredOperationsComplete) return "required_operation_incomplete";
            if (!input.RequiredQualityPassed) return "quality_gate_incomplete";
            return null;
        }
        return "invalid_batch_transition";
    }
}
