namespace His.Hope.PatientService.Application.DTOs;

public record CreatePatientRequest(
    string FirstName,
    string LastName,
    string? MiddleName,
    DateTime DateOfBirth,
    string GenderCode,
    string Phone,
    string? Email,
    string Street,
    string District,
    string City,
    string Province,
    string? PostalCode,
    string Country,
    string? InsuranceId,
    string? NationalId);

public record UpdatePatientRequest(
    string FirstName,
    string LastName,
    string? MiddleName,
    DateTime? DateOfBirth,
    string? GenderCode,
    string Phone,
    string? Email,
    string Street,
    string District,
    string City,
    string Province,
    string? PostalCode,
    string Country);
