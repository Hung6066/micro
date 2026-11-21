using His.Hope.ManufacturingService.Domain;

namespace His.Hope.ManufacturingService.Application;

public sealed record ProductionOrderValidationInput(string OrderNumber, string ProductSku, Guid RecipeId, decimal TargetQuantity, string OutputUom);
public sealed record ProductionBatchValidationInput(string OrderStatus, string BatchNumber, decimal PlannedQuantity);
public sealed record OperationMeasurementValidationInput(int Sequence, string ProcessStep, string Operator, decimal InputQuantity, decimal OutputQuantity, string QcStatus);
public sealed record BatchTransitionValidationInput(string CurrentStatus, string TargetStatus, bool HasOperations, bool RequiredOperationsComplete, bool RequiredQualityPassed, bool MachineAvailable);

public static class ProductionPolicy
{
    public static string? ValidateOrder(ProductionOrderValidationInput input) =>
        input.TargetQuantity <= 0 || string.IsNullOrWhiteSpace(input.OrderNumber) || string.IsNullOrWhiteSpace(input.ProductSku) ||
        input.RecipeId == Guid.Empty || string.IsNullOrWhiteSpace(input.OutputUom) ? ManufacturingErrorCodes.InvalidProductionOrder : null;

    public static string? ValidateBatch(ProductionBatchValidationInput input)
    {
        if (string.IsNullOrWhiteSpace(input.BatchNumber) || input.PlannedQuantity <= 0) return ManufacturingErrorCodes.InvalidProductionBatch;
        return input.OrderStatus == ManufacturingStatusCodes.Released ? null : "production_order_not_released";
    }

    public static string? ValidateOperation(ProductionBatchValidationInput batch, OperationMeasurementValidationInput input)
    {
        if (input.Sequence < 0 || input.InputQuantity <= 0 || input.OutputQuantity < 0 || input.OutputQuantity > input.InputQuantity ||
            string.IsNullOrWhiteSpace(input.ProcessStep) || string.IsNullOrWhiteSpace(input.Operator) || string.IsNullOrWhiteSpace(input.QcStatus))
            return "invalid_operation_measurement";
        return batch.OrderStatus is ManufacturingStatusCodes.Started or "Paused" ? null : ManufacturingErrorCodes.BatchNotStarted;
    }

    public static string? ValidateTransition(BatchTransitionValidationInput input)
    {
        if (input.TargetStatus == ManufacturingStatusCodes.Started && !input.MachineAvailable) return ManufacturingErrorCodes.MachineUnavailable;
        if ((input.CurrentStatus, input.TargetStatus) is (ManufacturingStatusCodes.Created, ManufacturingStatusCodes.Started) or (ManufacturingStatusCodes.Started, "Paused") or ("Paused", ManufacturingStatusCodes.Started)) return null;
        if ((input.CurrentStatus, input.TargetStatus) is (ManufacturingStatusCodes.Started, ManufacturingStatusCodes.Completed) or ("Paused", ManufacturingStatusCodes.Completed))
        {
            if (!input.HasOperations) return ManufacturingErrorCodes.RequiredOperationIncomplete;
            if (!input.RequiredOperationsComplete) return ManufacturingErrorCodes.RequiredOperationIncomplete;
            if (!input.RequiredQualityPassed) return ManufacturingErrorCodes.QualityGateIncomplete;
            return null;
        }
        return ManufacturingErrorCodes.InvalidBatchTransition;
    }
}
