namespace His.Hope.PatientService.Application.DTOs;

public class PatientDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public DateTime DateOfBirth { get; set; }
    public int Age => CalculateAge(DateOfBirth);
    public string GenderCode { get; set; } = string.Empty;
    public string GenderName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Street { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string Country { get; set; } = string.Empty;
    public string? BloodTypeCode { get; set; }
    public string? BloodTypeName { get; set; }
    public string? RaceCode { get; set; }
    public string? MaritalStatusCode { get; set; }
    public string? InsuranceId { get; set; }
    public string? NationalId { get; set; }
    public string? Occupation { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<AllergyDto> Allergies { get; set; } = [];
    public List<ConditionDto> Conditions { get; set; } = [];

    private static int CalculateAge(DateTime dateOfBirth)
    {
        var today = DateTime.Today;
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > today.AddYears(-age)) age--;
        return age;
    }
}

public class AllergyDto
{
    public Guid Id { get; set; }
    public string Allergen { get; set; } = string.Empty;
    public string? Reaction { get; set; }
    public string? Severity { get; set; }
    public DateTime RecordedDate { get; set; }
    public bool IsActive { get; set; }
}

public class ConditionDto
{
    public Guid Id { get; set; }
    public string ConditionName { get; set; } = string.Empty;
    public string? Icd10Code { get; set; }
    public DateTime? OnsetDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public bool IsChronic { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}
