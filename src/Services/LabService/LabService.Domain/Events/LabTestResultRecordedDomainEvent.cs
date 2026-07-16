using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.Events;

public class LabTestResultRecordedDomainEvent : DomainEvent
{
    public Guid LabOrderId { get; }
    public Guid LabTestId { get; }
    public string Value { get; }

    public LabTestResultRecordedDomainEvent(Guid labOrderId, Guid labTestId, string value)
    {
        LabOrderId = labOrderId;
        LabTestId = labTestId;
        Value = value;
    }
}
