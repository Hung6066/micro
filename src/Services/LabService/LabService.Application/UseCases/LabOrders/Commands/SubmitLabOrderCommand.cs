using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Application.Common.Exceptions;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.LabService.Application.UseCases.LabOrders.Commands;

public record SubmitLabOrderCommand(Guid Id) : IRequest<Unit>;

public class SubmitLabOrderCommandHandler : IRequestHandler<SubmitLabOrderCommand, Unit>
{
    private readonly ILabOrderRepository _labOrderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitLabOrderCommandHandler(ILabOrderRepository labOrderRepository)
    {
        _labOrderRepository = labOrderRepository;
        _unitOfWork = labOrderRepository.UnitOfWork;
    }

    public async Task<Unit> Handle(SubmitLabOrderCommand request,
        CancellationToken cancellationToken)
    {
        var labOrderId = LabOrderId.From(request.Id);
        var labOrder = await _labOrderRepository.GetByIdAsync(labOrderId, cancellationToken);

        if (labOrder is null)
            throw new NotFoundException(nameof(LabOrder), request.Id);

        labOrder.Submit();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
