using His.Hope.PharmacyService.Domain.Events;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PharmacyService.Domain.Aggregates;

public class Medication : AggregateRoot<MedicationId>
{
    public string Name { get; private set; }
    public string? GenericName { get; private set; }
    public string? BrandName { get; private set; }
    public string DosageForm { get; private set; }
    public string Strength { get; private set; }
    public string? Route { get; private set; }
    public string? Category { get; private set; }
    public string? Manufacturer { get; private set; }
    public bool RequiresPrescription { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Medication(
        MedicationId id,
        string name,
        string dosageForm,
        string strength)
        : base(id)
    {
        Name = Guard.Against.NullOrWhiteSpace(name, nameof(name));
        DosageForm = Guard.Against.NullOrWhiteSpace(dosageForm, nameof(dosageForm));
        Strength = Guard.Against.NullOrWhiteSpace(strength, nameof(strength));
        IsActive = true;
        CreatedAt = DateTime.UtcNow;

        AddDomainEvent(new MedicationCreatedDomainEvent(
            Id.Value, name, dosageForm, strength));
    }

    public static Medication Create(
        string name,
        string dosageForm,
        string strength)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.NullOrWhiteSpace(dosageForm, nameof(dosageForm));
        Guard.Against.NullOrWhiteSpace(strength, nameof(strength));

        var id = MedicationId.New();
        return new Medication(id, name, dosageForm, strength);
    }

    public void UpdateDetails(
        string name,
        string? genericName,
        string? brandName,
        string dosageForm,
        string strength,
        string? route,
        string? category,
        string? manufacturer,
        bool requiresPrescription)
    {
        Name = Guard.Against.NullOrWhiteSpace(name, nameof(name));
        GenericName = genericName;
        BrandName = brandName;
        DosageForm = Guard.Against.NullOrWhiteSpace(dosageForm, nameof(dosageForm));
        Strength = Guard.Against.NullOrWhiteSpace(strength, nameof(strength));
        Route = route;
        Category = category;
        Manufacturer = manufacturer;
        RequiresPrescription = requiresPrescription;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new MedicationUpdatedDomainEvent(Id.Value, name));
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new MedicationDeactivatedDomainEvent(Id.Value));
    }

    public void Reactivate()
    {
        if (IsActive) return;
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new MedicationReactivatedDomainEvent(Id.Value));
    }

    private Medication() { }
}
