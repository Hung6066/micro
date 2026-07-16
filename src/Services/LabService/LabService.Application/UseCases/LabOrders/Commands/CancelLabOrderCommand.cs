using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Application.Common.Exceptions;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.LabService.Application.UseCases.LabOrders.Commands;

public record CancelLabOrderCommand(Guid Id, string Reason) : IRequest<Unit>;

public class CancelLabOrderCommandHandler : IRequestHandler<CancelLabOrderCommand, Unit>
{
    private readonly ILabOrderRepository _labOrderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelLabOrderCommandHandler(ILabOrderRepository labOrderRepository)
    {
        _labOrderRepository = labOrderRepository;
        _unitOfWork = labOrderRepository.UnitOfWork;
    }

    public async Task<Unit> Handle(CancelLabOrderCommand request,
        CancellationToken cancellationToken)
    {
        var labOrderId = LabOrderId.From(request.Id);
        var labOrder = await _labOrderRepository.GetByIdAsync(labOrderId, cancellationToken);

        if (labOrder is null)
            throw new NotFoundException(nameof(LabOrder), request.Id);

        labOrder.Cancel(request.Reason);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
