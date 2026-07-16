namespace His.Hope.PharmacyService.Application.DTOs;

public class PrescriptionDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Guid ProviderId { get; set; }
    public Guid? MedicationId { get; set; }
    public string MedicationName { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public string DosageForm { get; set; } = string.Empty;
    public string DosageInstructions { get; set; } = string.Empty;
    public string? Route { get; set; }
    public int Quantity { get; set; }
    public int Refills { get; set; }
    public string? Notes { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public DateTime PrescribedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? FilledDate { get; set; }
    public DateTime? CancelledDate { get; set; }
    public string? CancellationReason { get; set; }
    public List<PrescriptionMedicationDto> Medications { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PrescriptionMedicationDto
{
    public Guid? MedicationId { get; set; }
    public string MedicationName { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public string DosageForm { get; set; } = string.Empty;
    public string DosageInstructions { get; set; } = string.Empty;
    public string? Route { get; set; }
    public int Quantity { get; set; }
    public int Refills { get; set; }
}
