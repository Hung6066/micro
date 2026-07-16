using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Domain.ValueObjects;
using His.Hope.LabService.Application.Common.Exceptions;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.LabService.Application.UseCases.LabOrders.Commands;

public record RecordLabOrderResultCommand(
    Guid OrderId,
    Guid TestId,
    string Value,
    string? AbnormalFlag,
    string? Notes) : IRequest<Unit>;

public class RecordLabOrderResultCommandHandler : IRequestHandler<RecordLabOrderResultCommand, Unit>
{
    private readonly ILabOrderRepository _labOrderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RecordLabOrderResultCommandHandler(ILabOrderRepository labOrderRepository)
    {
        _labOrderRepository = labOrderRepository;
        _unitOfWork = labOrderRepository.UnitOfWork;
    }

    public async Task<Unit> Handle(RecordLabOrderResultCommand request,
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

        var abnormalFlag = request.AbnormalFlag != null
            ? AbnormalFlag.FromCode(request.AbnormalFlag)
            : null;

        var resultStatus = LabResultStatus.FromCode("FINAL");

        var result = new LabResult(
            LabResultId.New(),
            request.Value,
            null,
            null,
            abnormalFlag,
            resultStatus,
            null,
            request.Notes);

        test.RecordResult(result);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
