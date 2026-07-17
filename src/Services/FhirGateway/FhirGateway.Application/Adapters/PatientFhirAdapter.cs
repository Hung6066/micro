using Hl7.Fhir.Model;

namespace His.Hope.FhirGateway.Application.Adapters;

/// <summary>
/// Maps His.Hope domain Patient data to HL7 FHIR R4 Patient resources.
/// </summary>
public static class PatientFhirAdapter
{
    /// <summary>
    /// Converts domain patient fields into a FHIR Patient resource.
    /// </summary>
    /// <param name="id">The His.Hope patient identifier.</param>
    /// <param name="firstName">Patient first name.</param>
    /// <param name="lastName">Patient last name.</param>
    /// <param name="middleName">Optional middle name.</param>
    /// <param name="dateOfBirth">Date of birth (UTC).</param>
    /// <param name="genderCode">Gender code (M/F/O).</param>
    /// <param name="phone">Primary phone number.</param>
    /// <param name="email">Primary email address.</param>
    /// <param name="isActive">Whether the patient record is active.</param>
    /// <returns>A populated FHIR Patient resource.</returns>
    public static Patient ToFhir(
        string id,
        string firstName,
        string lastName,
        string? middleName,
        DateTimeOffset dateOfBirth,
        string genderCode,
        string? phone,
        string? email,
        bool isActive)
    {
        var patient = new Patient
        {
            Id = id,
            Identifier = new List<Identifier>
            {
                new()
                {
                    System = "https://his.hope.vn/patient-id",
                    Value = id,
                    Type = new CodeableConcept
                    {
                        Coding = new List<Coding>
                        {
                            new("http://terminology.hl7.org/CodeSystem/v2-0203", "MR", "Medical Record Number")
                        }
                    }
                }
            },
            Name = new List<HumanName>
            {
                new()
                {
                    Family = lastName,
                    Given = new[] { firstName, middleName }.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray(),
                    Use = HumanName.NameUse.Official
                }
            },
            Gender = MapGender(genderCode),
            BirthDate = dateOfBirth.ToString("yyyy-MM-dd"),
            Active = isActive
        };

        if (!string.IsNullOrWhiteSpace(phone))
        {
            patient.Telecom.Add(new ContactPoint
            {
                System = ContactPoint.ContactPointSystem.Phone,
                Value = phone,
                Use = ContactPoint.ContactPointUse.Mobile
            });
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            patient.Telecom.Add(new ContactPoint
            {
                System = ContactPoint.ContactPointSystem.Email,
                Value = email,
                Use = ContactPoint.ContactPointUse.Home
            });
        }

        return patient;
    }

    private static AdministrativeGender? MapGender(string genderCode)
    {
        return genderCode?.ToUpperInvariant() switch
        {
            "M" => AdministrativeGender.Male,
            "F" => AdministrativeGender.Female,
            "O" => AdministrativeGender.Other,
            "UNKNOWN" => AdministrativeGender.Unknown,
            _ => null
        };
    }
}
