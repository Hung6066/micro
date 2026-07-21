using His.Hope.LabService.Application.DTOs;
using His.Hope.LabService.Application.UseCases.CriticalAlerts.Commands;
using His.Hope.LabService.Application.UseCases.CriticalAlerts.Queries;
using MediatR;

namespace His.Hope.LabService.Api.Endpoints;

public static class CriticalAlertEndpoints
{
    public static IEndpointRouteBuilder MapCriticalAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var criticalAlertRules = app.MapGroup("/api/v1/critical-alert-rules").RequireAuthorization("Permission:lab.manage");

        criticalAlertRules.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new GetCriticalAlertRulesQuery(), ct)))
            .WithOpenApi();

        criticalAlertRules.MapPost("/", async (CriticalAlertRuleUpsertRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var rule = await mediator.Send(new UpsertCriticalAlertRuleCommand(
                null,
                request.TestCode,
                request.TestName,
                request.Unit,
                request.LowCriticalValue,
                request.HighCriticalValue,
                request.IsActive), ct);

            return Results.Created($"/api/v1/critical-alert-rules/{rule.Id}", rule);
        })
        .WithOpenApi();

        criticalAlertRules.MapPut("/{id:guid}", async (Guid id, CriticalAlertRuleUpsertRequest request, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new UpsertCriticalAlertRuleCommand(
                id,
                request.TestCode,
                request.TestName,
                request.Unit,
                request.LowCriticalValue,
                request.HighCriticalValue,
                request.IsActive), ct)))
            .WithOpenApi();

        criticalAlertRules.MapDelete("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.Send(new DeleteCriticalAlertRuleCommand(id), ct);
            return Results.NoContent();
        }).WithOpenApi();

        var criticalAlerts = app.MapGroup("/api/v1/critical-alerts").RequireAuthorization("Permission:lab.view");

        criticalAlerts.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new GetCriticalAlertsQuery(), ct)))
            .WithOpenApi();

        criticalAlerts.MapPost("/{id:guid}/acknowledge", async (Guid id, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new AcknowledgeCriticalAlertCommand(id), ct)))
            .WithOpenApi();

        criticalAlerts.MapPost("/{id:guid}/resolve", async (Guid id, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new ResolveCriticalAlertCommand(id), ct)))
            .WithOpenApi();

        return app;
    }
}

public record CriticalAlertRuleUpsertRequest(
    string TestCode,
    string TestName,
    string? Unit,
    decimal? LowCriticalValue,
    decimal? HighCriticalValue,
    bool IsActive = true);
