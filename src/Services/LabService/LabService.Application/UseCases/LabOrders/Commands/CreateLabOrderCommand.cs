using His.Hope.LabService.Application.DTOs;
using MediatR;

namespace His.Hope.LabService.Application.UseCases.LabOrders.Commands;

public record TestItem(
    string TestCode,
    string TestName,
    string? SpecimenType);

public record CreateLabOrderCommand(
    Guid PatientId,
    Guid ProviderId,
    Guid? EncounterId,
    string PriorityCode,
    string? Notes,
    IReadOnlyList<TestItem> Tests) : IRequest<LabOrderDto>;
