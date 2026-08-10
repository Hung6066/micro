namespace His.Hope.PharmacyService.Application.DTOs;

public class MedicationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? BrandName { get; set; }
    public string DosageForm { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public string? Route { get; set; }
    public string? Category { get; set; }
    public string? Manufacturer { get; set; }
    public bool RequiresPrescription { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
