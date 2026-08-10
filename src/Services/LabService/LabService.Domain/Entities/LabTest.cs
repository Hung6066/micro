using His.Hope.LabService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.LabService.Domain.Entities;

public class LabTest : Entity<LabTestId>
{
    public LabOrderId LabOrderId { get; private set; }
    public string TestCode { get; private set; }
    public string TestName { get; private set; }
    public string? SpecimenType { get; private set; }
    public LabTestStatus Status { get; private set; }
    public LabResult? Result { get; private set; }
    public DateTime OrderedAt { get; private set; }
    public DateTime? CollectedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private LabTest(
        LabTestId id,
        LabOrderId labOrderId,
        string testCode,
        string testName,
        string? specimenType)
        : base(id)
    {
        LabOrderId = Guard.Against.Null(labOrderId, nameof(labOrderId));
        TestCode = Guard.Against.NullOrWhiteSpace(testCode, nameof(testCode));
        TestName = Guard.Against.NullOrWhiteSpace(testName, nameof(testName));
        SpecimenType = specimenType;
        Status = LabTestStatus.Ordered;
        OrderedAt = DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
    }

    public static LabTest Create(
        LabOrderId labOrderId,
        string testCode,
        string testName,
        string? specimenType)
    {
        Guard.Against.NullOrWhiteSpace(testCode, nameof(testCode));
        Guard.Against.NullOrWhiteSpace(testName, nameof(testName));

        var id = LabTestId.New();
        return new LabTest(id, labOrderId, testCode, testName, specimenType);
    }

    public void MarkCollected(DateTime? collectedAt = null)
    {
        if (Status == LabTestStatus.Collected)
            throw new DomainException("Lab test has already been collected.");
        if (Status == LabTestStatus.Cancelled)
            throw new DomainException("Cannot collect a cancelled lab test.");
        if (Status == LabTestStatus.Resulted)
            throw new DomainException("Cannot collect a resulted lab test.");

        Status = LabTestStatus.Collected;
        CollectedAt = collectedAt ?? DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkInProgress()
    {
        if (Status == LabTestStatus.InProgress)
            throw new DomainException("Lab test is already in progress.");
        if (Status == LabTestStatus.Cancelled)
            throw new DomainException("Cannot start a cancelled lab test.");
        if (Status == LabTestStatus.Resulted)
            throw new DomainException("Cannot start a resulted lab test.");

        Status = LabTestStatus.InProgress;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordResult(LabResult result)
    {
        if (Status == LabTestStatus.Cancelled)
            throw new DomainException("Cannot record result for a cancelled lab test.");
        if (Status == LabTestStatus.Ordered)
            throw new DomainException("Cannot record result before collecting sample.");

        Result = Guard.Against.Null(result, nameof(result));
        Status = LabTestStatus.Resulted;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == LabTestStatus.Cancelled)
            throw new DomainException("Lab test has already been cancelled.");
        if (Status == LabTestStatus.Resulted)
            throw new DomainException("Cannot cancel a resulted lab test.");

        Status = LabTestStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    private LabTest() { }
}

public class LabResult
{
    public LabResultId LabResultId { get; private set; }
    public string Value { get; private set; }
    public string? Unit { get; private set; }
    public string? ReferenceRange { get; private set; }
    public AbnormalFlag? AbnormalFlag { get; private set; }
    public LabResultStatus ResultStatus { get; private set; }
    public DateTime ResultedAt { get; private set; }
    public string? PerformedBy { get; private set; }
    public string? Notes { get; private set; }

    public LabResult(
        LabResultId labResultId,
        string value,
        string? unit,
        string? referenceRange,
        AbnormalFlag? abnormalFlag,
        LabResultStatus resultStatus,
        string? performedBy,
        string? notes)
    {
        LabResultId = Guard.Against.Null(labResultId, nameof(labResultId));
        Value = Guard.Against.NullOrWhiteSpace(value, nameof(value));
        Unit = unit;
        ReferenceRange = referenceRange;
        AbnormalFlag = abnormalFlag;
        ResultStatus = Guard.Against.Null(resultStatus, nameof(resultStatus));
        ResultedAt = DateTime.UtcNow;
        PerformedBy = performedBy;
        Notes = notes;
    }

    private LabResult() { }
}
