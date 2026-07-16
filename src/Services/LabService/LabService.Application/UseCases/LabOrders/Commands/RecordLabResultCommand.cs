using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Domain.ValueObjects;
using His.Hope.LabService.Application.Common.Exceptions;
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
    private readonly IUnitOfWork _unitOfWork;

    public RecordLabResultCommandHandler(ILabOrderRepository labOrderRepository)
    {
        _labOrderRepository = labOrderRepository;
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

        return Unit.Value;
    }
}
