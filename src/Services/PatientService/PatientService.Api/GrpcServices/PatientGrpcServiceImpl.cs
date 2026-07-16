using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using His.Hope.PatientGrpc;
using His.Hope.PatientService.Domain.Repositories;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Application.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace His.Hope.PatientService.Api.GrpcServices;

[Authorize]
public class PatientGrpcServiceImpl : PatientGrpcService.PatientGrpcServiceBase
{
    private readonly IPatientRepository _patientRepository;
    private readonly IMapper _mapper;

    public PatientGrpcServiceImpl(IPatientRepository patientRepository, IMapper mapper)
    {
        _patientRepository = patientRepository;
        _mapper = mapper;
    }

    public override async Task<PatientResponse> GetPatient(PatientRequest request,
        ServerCallContext context)
    {
        var patientId = PatientId.From(Guid.Parse(request.Id));
        var patient = await _patientRepository.GetByIdAsync(patientId);

        if (patient is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Patient not found"));

        return MapToResponse(patient);
    }

    public override async Task<PatientListResponse> SearchPatients(
        PatientSearchRequest request, ServerCallContext context)
    {
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;
        var (items, totalCount) = await _patientRepository.SearchAsync(
            request.SearchTerm, page, pageSize, context.CancellationToken);

        var response = new PatientListResponse();
        response.Patients.AddRange(items.Select(MapToResponse));
        response.TotalCount = totalCount;
        response.Page = page;
        response.PageSize = pageSize;
        return response;
    }

    public override async Task<PatientExistsResponse> CheckPatientExists(
        PatientExistsRequest request, ServerCallContext context)
    {
        var patientId = PatientId.From(Guid.Parse(request.Id));
        var exists = await _patientRepository.ExistsAsync(patientId);

        return new PatientExistsResponse { Exists = exists };
    }

    private static PatientResponse MapToResponse(Domain.Aggregates.Patient patient) =>
        new()
        {
            Id = patient.Id.Value.ToString(),
            FullName = patient.Name.FullName,
            FirstName = patient.Name.FirstName,
            LastName = patient.Name.LastName,
            MiddleName = patient.Name.MiddleName ?? string.Empty,
            DateOfBirth = patient.DateOfBirth.ToTimestamp(),
            GenderCode = patient.Gender.Code,
            GenderName = patient.Gender.Name,
            Phone = patient.ContactInfo.Phone,
            Email = patient.ContactInfo.Email ?? string.Empty,
            IsActive = patient.IsActive,
            CreatedAt = patient.CreatedAt.ToTimestamp(),
            UpdatedAt = patient.UpdatedAt?.ToTimestamp()
        };
}
