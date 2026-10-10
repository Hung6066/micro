using His.Hope.ManufacturingService.Application;

namespace ManufacturingService.Application.Tests;

public sealed class ProductionPolicyTests
{
    [Fact]
    public void AllowsStartWithoutMachine()
    {
        var input = new BatchTransitionValidationInput("Created", "Started", false, false, false, true);

        Assert.Null(ProductionPolicy.ValidateTransition(input));
    }

    [Fact]
    public void RequiresCompletedPassingOperationsBeforeCompletion()
    {
        var input = new BatchTransitionValidationInput("Started", "Completed", true, true, false, true);

        Assert.Equal("quality_gate_incomplete", ProductionPolicy.ValidateTransition(input));
    }

    [Fact]
    public void RejectsOperationOutputAboveInput()
    {
        var batch = new ProductionBatchValidationInput("Started", "batch", 10m);
        var operation = new OperationMeasurementValidationInput(1, "Dry", "operator", 10m, 11m, "Pass");

        Assert.Equal("invalid_operation_measurement", ProductionPolicy.ValidateOperation(batch, operation));
    }

    [Fact]
    public void RejectsUnreleasedOrderForBatch()
    {
        var input = new ProductionBatchValidationInput("Planned", "B-1", 10m);

        Assert.Equal("production_order_not_released", ProductionPolicy.ValidateBatch(input));
    }
}
