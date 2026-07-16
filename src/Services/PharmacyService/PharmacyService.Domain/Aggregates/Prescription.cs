using His.Hope.PharmacyService.Domain.Events;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.PharmacyService.Domain.Aggregates;

public class Prescription : AggregateRoot<PrescriptionId>
{
    public Guid PatientId { get; private set; }
    public Guid ProviderId { get; private set; }
    public Guid? MedicationId { get; private set; }
    public string MedicationName { get; private set; }
    public string Strength { get; private set; }
    public string DosageForm { get; private set; }
    public string DosageInstructions { get; private set; }
    public string? Route { get; private set; }
    public int Quantity { get; private set; }
    public int Refills { get; private set; }
    public string? Notes { get; private set; }
    public PrescriptionStatus Status { get; private set; }
    public DateTime PrescribedDate { get; private set; }
    public DateTime? ExpiryDate { get; private set; }
    public DateTime? FilledDate { get; private set; }
    public DateTime? CancelledDate { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Prescription(
        PrescriptionId id,
        Guid patientId,
        Guid providerId,
        Guid? medicationId,
        string medicationName,
        string strength,
        string dosageForm,
        string dosageInstructions,
        string? route,
        int quantity,
        int refills,
        string? notes,
        DateTime? expiryDate)
        : base(id)
    {
        PatientId = patientId;
        ProviderId = providerId;
        MedicationId = medicationId;
        MedicationName = Guard.Against.NullOrWhiteSpace(medicationName, nameof(medicationName));
        Strength = Guard.Against.NullOrWhiteSpace(strength, nameof(strength));
        DosageForm = Guard.Against.NullOrWhiteSpace(dosageForm, nameof(dosageForm));
        DosageInstructions = Guard.Against.NullOrWhiteSpace(dosageInstructions, nameof(dosageInstructions));
        Route = route;
        Quantity = quantity > 0 ? quantity
            : throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        Refills = refills;
        Notes = notes;
        Status = PrescriptionStatus.Prescribed;
        PrescribedDate = DateTime.UtcNow;
        ExpiryDate = expiryDate;
        CreatedAt = DateTime.UtcNow;

        AddDomainEvent(new PrescriptionCreatedDomainEvent(
            Id.Value, patientId, providerId, medicationName, quantity));
    }

    public static Prescription Create(
        Guid patientId,
        Guid providerId,
        Guid? medicationId,
        string medicationName,
        string strength,
        string dosageForm,
        string dosageInstructions,
        string? route,
        int quantity,
        int refills,
        string? notes,
        DateTime? expiryDate)
    {
        Guard.Against.NullOrWhiteSpace(medicationName, nameof(medicationName));
        Guard.Against.NullOrWhiteSpace(strength, nameof(strength));
        Guard.Against.NullOrWhiteSpace(dosageForm, nameof(dosageForm));
        Guard.Against.NullOrWhiteSpace(dosageInstructions, nameof(dosageInstructions));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");

        var id = PrescriptionId.New();
        return new Prescription(
            id, patientId, providerId, medicationId,
            medicationName, strength, dosageForm, dosageInstructions,
            route, quantity, refills, notes, expiryDate);
    }

    public void Fill()
    {
        if (Status == PrescriptionStatus.Filled)
            throw new DomainException("Prescription has already been filled.");

        if (Status == PrescriptionStatus.Cancelled)
            throw new DomainException("Cannot fill a cancelled prescription.");

        if (Status == PrescriptionStatus.Expired)
            throw new DomainException("Cannot fill an expired prescription.");

        Status = PrescriptionStatus.Filled;
        FilledDate = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PrescriptionFilledDomainEvent(Id.Value, FilledDate.Value));
    }

    public void Cancel(string reason)
    {
        if (Status == PrescriptionStatus.Cancelled)
            throw new DomainException("Prescription has already been cancelled.");

        if (Status == PrescriptionStatus.Filled)
            throw new DomainException("Cannot cancel a filled prescription.");

        Status = PrescriptionStatus.Cancelled;
        CancellationReason = Guard.Against.NullOrWhiteSpace(reason, nameof(reason));
        CancelledDate = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PrescriptionCancelledDomainEvent(Id.Value, reason));
    }

    private Prescription() { }
}
