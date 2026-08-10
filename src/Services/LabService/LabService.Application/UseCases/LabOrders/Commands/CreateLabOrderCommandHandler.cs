using AutoMapper;
using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Domain.ValueObjects;
using His.Hope.LabService.Application.DTOs;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.LabService.Application.UseCases.LabOrders.Commands;

public class CreateLabOrderCommandHandler : IRequestHandler<CreateLabOrderCommand, LabOrderDto>
{
    private readonly ILabOrderRepository _labOrderRepository;
    private readonly IMapper _mapper;
    private readonly DomainEventDispatcher _eventDispatcher;
    private readonly IUnitOfWork _unitOfWork;

    public CreateLabOrderCommandHandler(
        ILabOrderRepository labOrderRepository,
        IMapper mapper,
        DomainEventDispatcher eventDispatcher)
    {
        _labOrderRepository = labOrderRepository;
        _mapper = mapper;
        _eventDispatcher = eventDispatcher;
        _unitOfWork = labOrderRepository.UnitOfWork;
    }

    public async Task<LabOrderDto> Handle(CreateLabOrderCommand request,
        CancellationToken cancellationToken)
    {
        var priority = LabOrderPriority.FromCode(request.PriorityCode);

        var labOrder = LabOrder.Create(
            request.PatientId,
            request.ProviderId,
            request.EncounterId,
            priority,
            request.Notes);

        foreach (var testItem in request.Tests)
        {
            var test = LabTest.Create(
                labOrder.Id,
                testItem.TestCode,
                testItem.TestName,
                testItem.SpecimenType);
            labOrder.AddTest(test);
        }

        await _labOrderRepository.AddAsync(labOrder, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<LabOrderDto>(labOrder);
    }
}
