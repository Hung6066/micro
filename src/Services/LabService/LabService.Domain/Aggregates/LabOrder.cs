using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Events;
using His.Hope.LabService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.LabService.Domain.Aggregates;

public class LabOrder : AggregateRoot<LabOrderId>
{
    private readonly List<LabTest> _tests = [];

    public Guid PatientId { get; private set; }
    public Guid ProviderId { get; private set; }
    public Guid? EncounterId { get; private set; }
    public DateTime OrderDate { get; private set; }
    public LabOrderStatus Status { get; private set; }
    public LabOrderPriority Priority { get; private set; }
    public string? Notes { get; private set; }
    public IReadOnlyCollection<LabTest> RequestedTests => _tests.AsReadOnly();
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private LabOrder(
        LabOrderId id,
        Guid patientId,
        Guid providerId,
        Guid? encounterId,
        LabOrderPriority priority,
        string? notes)
        : base(id)
    {
        PatientId = patientId;
        ProviderId = providerId;
        EncounterId = encounterId;
        Priority = Guard.Against.Null(priority, nameof(priority));
        Notes = notes;
        Status = LabOrderStatus.Pending;
        OrderDate = DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;

        AddDomainEvent(new LabOrderCreatedDomainEvent(Id.Value, patientId, providerId));
    }

    public static LabOrder Create(
        Guid patientId,
        Guid providerId,
        Guid? encounterId,
        LabOrderPriority priority,
        string? notes)
    {
        var id = LabOrderId.New();
        return new LabOrder(id, patientId, providerId, encounterId, priority, notes);
    }

    public void AddTest(LabTest test)
    {
        if (Status != LabOrderStatus.Pending)
            throw new DomainException("Cannot add tests to a non-pending lab order.");

        Guard.Against.Null(test, nameof(test));
        _tests.Add(test);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Submit()
    {
        if (Status == LabOrderStatus.Submitted)
            throw new DomainException("Lab order has already been submitted.");

        if (Status == LabOrderStatus.Cancelled)
            throw new DomainException("Cannot submit a cancelled lab order.");

        if (Status == LabOrderStatus.Completed)
            throw new DomainException("Cannot submit a completed lab order.");

        if (_tests.Count == 0)
            throw new DomainException("Cannot submit a lab order with no tests.");

        Status = LabOrderStatus.Submitted;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new LabOrderSubmittedDomainEvent(Id.Value));
    }

    public void Cancel(string reason)
    {
        if (Status == LabOrderStatus.Cancelled)
            throw new DomainException("Lab order has already been cancelled.");

        if (Status == LabOrderStatus.Completed)
            throw new DomainException("Cannot cancel a completed lab order.");

        Status = LabOrderStatus.Cancelled;
        Notes = string.IsNullOrWhiteSpace(reason) ? Notes : reason;
        UpdatedAt = DateTime.UtcNow;

        foreach (var test in _tests.Where(t => t.Status != LabTestStatus.Resulted && t.Status != LabTestStatus.Cancelled))
        {
            test.Cancel();
        }

        AddDomainEvent(new LabOrderCancelledDomainEvent(Id.Value, reason));
    }

    private LabOrder() { }
}
