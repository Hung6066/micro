using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Domain.ValueObjects;
using His.Hope.LabService.Application.Common.Exceptions;
using His.Hope.LabService.Application.Common.Abstractions;
using His.Hope.LabService.Application.DTOs;
using His.Hope.LabService.Application.Services;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.LabService.Application.UseCases.LabOrders.Commands;

public record RecordLabResultCommand(
    Guid OrderId,
    Guid TestId,
    string Value,
    string? Unit,
    string? ReferenceRange,
    string? AbnormalFlagCode,
    string ResultStatusCode,
    string? PerformedBy,
    string? Notes) : IRequest<Unit>;

public class RecordLabResultCommandHandler : IRequestHandler<RecordLabResultCommand, Unit>
{
    private readonly ILabOrderRepository _labOrderRepository;
    private readonly CriticalAlertEvaluator _criticalAlertEvaluator;
    private readonly ICriticalAlertRealtimePublisher _criticalAlertRealtimePublisher;
    private readonly IUnitOfWork _unitOfWork;

    public RecordLabResultCommandHandler(
        ILabOrderRepository labOrderRepository,
        CriticalAlertEvaluator criticalAlertEvaluator,
        ICriticalAlertRealtimePublisher criticalAlertRealtimePublisher)
    {
        _labOrderRepository = labOrderRepository;
        _criticalAlertEvaluator = criticalAlertEvaluator;
        _criticalAlertRealtimePublisher = criticalAlertRealtimePublisher;
        _unitOfWork = labOrderRepository.UnitOfWork;
    }

    public async Task<Unit> Handle(RecordLabResultCommand request,
        CancellationToken cancellationToken)
    {
        var labOrderId = LabOrderId.From(request.OrderId);
        var labOrder = await _labOrderRepository.GetByIdAsync(labOrderId, cancellationToken);

        if (labOrder is null)
            throw new NotFoundException(nameof(LabOrder), request.OrderId);

        var test = labOrder.RequestedTests
            .FirstOrDefault(t => t.Id == LabTestId.From(request.TestId));

        if (test is null)
            throw new NotFoundException(nameof(LabTest), request.TestId);

        var abnormalFlag = request.AbnormalFlagCode != null
            ? AbnormalFlag.FromCode(request.AbnormalFlagCode)
            : null;

        var resultStatus = LabResultStatus.FromCode(request.ResultStatusCode);

        var result = new LabResult(
            LabResultId.New(),
            request.Value,
            request.Unit,
            request.ReferenceRange,
            abnormalFlag,
            resultStatus,
            request.PerformedBy,
            request.Notes);

        test.RecordResult(result);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var alert = await _criticalAlertEvaluator.EvaluateAsync(labOrder, test, result, cancellationToken);
        if (alert is not null)
        {
            var eventName = GetRealtimeEventName(alert);

            if (eventName == "criticalAlertResolved")
                await _criticalAlertRealtimePublisher.PublishResolvedAsync(alert, cancellationToken);
            else if (eventName == "criticalAlertCreated")
                await _criticalAlertRealtimePublisher.PublishCreatedAsync(alert, cancellationToken);
            else
                await _criticalAlertRealtimePublisher.PublishUpdatedAsync(alert, cancellationToken);
        }

        return Unit.Value;
    }

    private static string GetRealtimeEventName(CriticalAlertDto alert)
    {
        var latestAction = alert.AuditEntries.LastOrDefault()?.Action;

        return latestAction switch
        {
            "Resolved" => "criticalAlertResolved",
            "Created" => "criticalAlertCreated",
            _ => "criticalAlertUpdated"
        };
    }
}
