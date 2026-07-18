using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.Infrastructure.AsyncOperations;

/// <summary>
/// Maps the <c>GET /api/v1/operations/{id}</c> endpoint for polling
/// the status of async operations.
///
/// Usage in Program.cs:
/// <code>
/// var app = builder.Build();
/// app.MapOperationStatusEndpoints();
/// </code>
/// </summary>
public static class OperationEndpoints
{
    /// <summary>
    /// Maps the operation status polling endpoint.
    /// </summary>
    public static IEndpointRouteBuilder MapOperationStatusEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/operations/{id:guid}", async (
            Guid id,
            OperationStatusDbContext dbContext,
            HttpContext httpContext) =>
        {
            var record = await dbContext.OperationStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id, httpContext.RequestAborted);

            if (record is null)
            {
                return Results.NotFound(new
                {
                    error = $"Operation with id '{id}' not found.",
                    id
                });
            }

            // Build response based on status
            object response = record.Status switch
            {
                OperationStatusValue.Queued => new
                {
                    operationId = record.Id,
                    operationType = record.OperationType,
                    status = record.Status,
                    progress = record.Progress,
                    createdAt = record.CreatedAt
                },

                OperationStatusValue.Processing => new
                {
                    operationId = record.Id,
                    operationType = record.OperationType,
                    status = record.Status,
                    progress = record.Progress,
                    createdAt = record.CreatedAt
                },

                OperationStatusValue.Completed => new
                {
                    operationId = record.Id,
                    operationType = record.OperationType,
                    status = record.Status,
                    progress = record.Progress,
                    createdAt = record.CreatedAt,
                    completedAt = record.CompletedAt,
                    result = record.ResultData != null
                        ? System.Text.Json.JsonSerializer.Deserialize<object>(record.ResultData)
                        : null
                },

                OperationStatusValue.Failed => new
                {
                    operationId = record.Id,
                    operationType = record.OperationType,
                    status = record.Status,
                    progress = record.Progress,
                    createdAt = record.CreatedAt,
                    completedAt = record.CompletedAt,
                    error = record.ErrorMessage
                },

                _ => new
                {
                    operationId = record.Id,
                    operationType = record.OperationType,
                    status = record.Status,
                    progress = record.Progress,
                    createdAt = record.CreatedAt
                }
            };

            return Results.Ok(response);
        })
        .WithName("GetOperationStatus")
        .WithDisplayName("Get Operation Status")
        .WithTags("Operations")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
