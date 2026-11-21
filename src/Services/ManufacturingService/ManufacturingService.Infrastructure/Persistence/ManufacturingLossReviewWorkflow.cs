using System.Text.Json;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Domain;
using Microsoft.EntityFrameworkCore;

public sealed partial class PostgresManufacturingStore
{
    public async Task<(LossReviewDto? Review, string? Error)> ReviewLossAsync(string tenantKey, Guid batchId, Guid operationId, LossReviewRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reviewer) || request.Decision is not (ManufacturingStatusCodes.Approved or ManufacturingStatusCodes.Rejected))
            return (null, "invalid_loss_review");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var batch = await db.ProductionBatches.SingleOrDefaultAsync(x => x.Id == batchId && x.TenantKey == tenantKey, cancellationToken);
        if (batch is null) return (null, ManufacturingErrorCodes.ProductionBatchNotFound);
        var operation = await db.OperationExecutions.SingleOrDefaultAsync(x => x.Id == operationId && x.ProductionBatchId == batchId, cancellationToken);
        if (operation is null) return (null, ManufacturingErrorCodes.OperationNotFound);
        var review = await db.LossReviews.SingleOrDefaultAsync(x => x.OperationExecutionId == operationId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (review is null)
        {
            review = new ManufacturingLossReviewEntity
            {
                Id = Guid.NewGuid(), TenantKey = tenantKey, ProductionBatchId = batchId, OperationExecutionId = operationId,
                Decision = request.Decision, Reviewer = request.Reviewer.Trim(), Notes = request.Notes?.Trim(), ReviewedAt = now
            };
            db.LossReviews.Add(review);
        }
        else
        {
            review.Decision = request.Decision;
            review.Reviewer = request.Reviewer.Trim();
            review.Notes = request.Notes?.Trim();
            review.ReviewedAt = now;
        }
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = "Manufacturing.LossThresholdReviewed.v1",
            Content = JsonSerializer.Serialize(new { eventId = review.Id, schemaVersion = 1, occurredAt = now, correlationId = batchId, productionBatchId = batchId, operationId, tenantKey, decision = review.Decision, reviewer = review.Reviewer }),
            OccurredOn = now.UtcDateTime, Status = ManufacturingStatusCodes.Pending
        });
        await db.SaveChangesAsync(cancellationToken);
        return (new LossReviewDto(review.Id, review.TenantKey, review.ProductionBatchId, review.OperationExecutionId, review.Decision, review.Reviewer, review.Notes, review.ReviewedAt), null);
    }
}
