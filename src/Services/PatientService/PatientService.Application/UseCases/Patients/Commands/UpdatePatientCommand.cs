using His.Hope.PatientService.Application.DTOs;
using MediatR;

namespace His.Hope.PatientService.Application.UseCases.Patients.Commands;

public record UpdatePatientCommand(
    Guid Id,
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
    string Country) : IRequest<PatientDto>;
