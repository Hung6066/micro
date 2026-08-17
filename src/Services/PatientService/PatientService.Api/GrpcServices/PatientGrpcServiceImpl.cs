using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using His.Hope.PatientGrpc;
using His.Hope.PatientService.Domain.Repositories;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Application.DTOs;
using His.Hope.PatientService.Infrastructure.Persistence;
using His.Hope.Authorization;
using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace His.Hope.PatientService.Api.GrpcServices;

[Authorize(Policy = AuthorizationPolicyNames.Permissions.PatientsView)]
public class PatientGrpcServiceImpl : PatientGrpcService.PatientGrpcServiceBase
{
    private readonly IPatientRepository _patientRepository;
    private readonly IMapper _mapper;
    private readonly PatientDbContext _db;
    private readonly IResourceAuthorizationEvaluator _authorization;

    public PatientGrpcServiceImpl(
        IPatientRepository patientRepository,
        IMapper mapper,
        PatientDbContext db,
        IResourceAuthorizationEvaluator authorization)
    {
        _patientRepository = patientRepository;
        _mapper = mapper;
        _db = db;
        _authorization = authorization;
    }

    public override async Task<PatientResponse> GetPatient(PatientRequest request,
        ServerCallContext context)
    {
        var patientId = PatientId.From(ParseGuidOrThrow(request.Id, "Patient id"));
        await EnsureResourceAccessAsync(patientId, context);
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
        var accessScope = FacilityAccessScope.FromPrincipal(context.GetHttpContext().User);
        var (items, totalCount) = await _patientRepository.SearchAsync(
            request.SearchTerm,
            page,
            pageSize,
            accessScope.FacilityIds,
            accessScope.IsCrossFacility,
            context.CancellationToken);

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
        var patientId = PatientId.From(ParseGuidOrThrow(request.Id, "Patient id"));
        await EnsureResourceAccessAsync(patientId, context);
        var exists = await _patientRepository.ExistsAsync(patientId);

        return new PatientExistsResponse { Exists = exists };
    }

    private async Task EnsureResourceAccessAsync(PatientId patientId, ServerCallContext context)
    {
        var http = context.GetHttpContext();
        var decision = await _authorization.EvaluateResourceAsync(
            _db.Patients,
            patient => patient.Id == patientId,
            patient => patient.FacilityId,
            http.User,
            HisHopePermissions.Patients.View,
            "patient",
            patientId.Value.ToString("D"),
            context.CancellationToken);
        if (!decision.Allowed)
            throw new RpcException(new Status(StatusCode.NotFound, "Patient not found"));
    }

    private static Guid ParseGuidOrThrow(string value, string fieldName)
    {
        if (Guid.TryParse(value, out var parsed))
            return parsed;

        throw new RpcException(new Status(StatusCode.InvalidArgument, $"{fieldName} must be a valid GUID."));
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static PatientResponse MapToResponse(Domain.Aggregates.Patient patient) =>
        new()
        {
            Id = patient.Id.Value.ToString(),
            FullName = patient.Name.FullName,
            FirstName = patient.Name.FirstName,
            LastName = patient.Name.LastName,
            MiddleName = patient.Name.MiddleName ?? string.Empty,
            DateOfBirth = AsUtc(patient.DateOfBirth).ToTimestamp(),
            GenderCode = patient.Gender.Code,
            GenderName = patient.Gender.Name,
            Phone = patient.ContactInfo.Phone,
            Email = patient.ContactInfo.Email ?? string.Empty,
            IsActive = patient.IsActive,
            CreatedAt = AsUtc(patient.CreatedAt).ToTimestamp(),
            UpdatedAt = patient.UpdatedAt is { } updatedAt ? AsUtc(updatedAt).ToTimestamp() : null
        };
}
