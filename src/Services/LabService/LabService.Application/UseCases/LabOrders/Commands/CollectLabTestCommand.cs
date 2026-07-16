using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Application.Common.Exceptions;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.LabService.Application.UseCases.LabOrders.Commands;

public record CollectLabTestCommand(Guid OrderId, Guid TestId, DateTime? CollectedAt) : IRequest<Unit>;

public class CollectLabTestCommandHandler : IRequestHandler<CollectLabTestCommand, Unit>
{
    private readonly ILabOrderRepository _labOrderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CollectLabTestCommandHandler(ILabOrderRepository labOrderRepository)
    {
        _labOrderRepository = labOrderRepository;
        _unitOfWork = labOrderRepository.UnitOfWork;
    }

    public async Task<Unit> Handle(CollectLabTestCommand request,
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

        test.MarkCollected(request.CollectedAt);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
