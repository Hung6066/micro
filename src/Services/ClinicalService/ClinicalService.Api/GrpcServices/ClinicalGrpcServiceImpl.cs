using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using His.Hope.ClinicalGrpc;
using His.Hope.ClinicalService.Application.DTOs;
using His.Hope.ClinicalService.Application.UseCases.Encounters.Queries;
using His.Hope.ClinicalService.Domain.Repositories;
using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.ClinicalService.Infrastructure.Persistence;
using His.Hope.Authorization;
using His.Hope.SharedKernel.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace His.Hope.ClinicalService.Api.GrpcServices;

[Authorize(Policy = AuthorizationPolicyNames.Permissions.ClinicalView)]
public class ClinicalGrpcServiceImpl : ClinicalGrpcService.ClinicalGrpcServiceBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ClinicalGrpcServiceImpl> _logger;
    private readonly ClinicalDbContext _db;
    private readonly IResourceAuthorizationEvaluator _authorization;

    public ClinicalGrpcServiceImpl(
        IMediator mediator,
        ILogger<ClinicalGrpcServiceImpl> logger,
        ClinicalDbContext db,
        IResourceAuthorizationEvaluator authorization)
    {
        _mediator = mediator;
        _logger = logger;
        _db = db;
        _authorization = authorization;
    }

    public override async Task<EncounterResponse> GetEncounter(EncounterRequest request,
        ServerCallContext context)
    {
        var encounterId = ParseGuidOrThrow(request.Id, "Encounter id");
        await EnsureResourceAccessAsync(encounterId, context);
        var encounter = await _mediator.Send(
            new GetEncounterByIdQuery(encounterId),
            context.CancellationToken);

        if (encounter is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Encounter not found"));

        return MapToResponse(encounter);
    }

    public override async Task<EncounterListResponse> GetPatientEncounters(
        PatientEncountersRequest request, ServerCallContext context)
    {
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;
        var accessScope = FacilityAccessScope.FromPrincipal(context.GetHttpContext().User);
        var result = await _mediator.Send(
            new SearchEncountersQuery(request.PatientId, page, pageSize,
                accessScope.FacilityIds, accessScope.IsCrossFacility),
            context.CancellationToken);

        var response = new EncounterListResponse();
        response.Encounters.AddRange(result.Items.Select(MapToResponse));
        response.TotalCount = result.TotalCount;
        response.Page = page;
        response.PageSize = pageSize;
        return response;
    }

    public override async Task<EncounterListResponse> SearchEncounters(
        EncounterSearchRequest request, ServerCallContext context)
    {
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;
        var accessScope = FacilityAccessScope.FromPrincipal(context.GetHttpContext().User);
        var result = await _mediator.Send(
            new SearchEncountersQuery(request.SearchTerm, page, pageSize,
                accessScope.FacilityIds, accessScope.IsCrossFacility),
            context.CancellationToken);

        var response = new EncounterListResponse();
        response.Encounters.AddRange(result.Items.Select(MapToResponse));
        response.TotalCount = result.TotalCount;
        response.Page = page;
        response.PageSize = pageSize;
        return response;
    }

    public override async Task<EncounterExistsResponse> CheckEncounterExists(
        EncounterExistsRequest request, ServerCallContext context)
    {
        var encounterId = ParseGuidOrThrow(request.Id, "Encounter id");
        await EnsureResourceAccessAsync(encounterId, context);
        var encounter = await _mediator.Send(
            new GetEncounterByIdQuery(encounterId),
            context.CancellationToken);

        return new EncounterExistsResponse { Exists = encounter is not null };
    }

    private async Task EnsureResourceAccessAsync(Guid encounterId, ServerCallContext context)
    {
        var decision = await _authorization.EvaluateResourceAsync(
            _db.Encounters,
            encounter => encounter.Id == EncounterId.From(encounterId),
            encounter => encounter.FacilityId,
            context.GetHttpContext().User,
            HisHopePermissions.Clinical.View,
            "encounter",
            encounterId.ToString("D"),
            context.CancellationToken);
        if (!decision.Allowed)
            throw new RpcException(new Status(StatusCode.NotFound, "Encounter not found"));
    }

    private static Guid ParseGuidOrThrow(string value, string fieldName)
    {
        if (Guid.TryParse(value, out var parsed))
            return parsed;

        throw new RpcException(new Status(StatusCode.InvalidArgument, $"{fieldName} must be a valid GUID."));
    }

    private static EncounterResponse MapToResponse(EncounterDto encounter) =>
        new()
        {
            Id = encounter.Id.ToString(),
            PatientId = encounter.PatientId.ToString(),
            ProviderId = encounter.ProviderId.ToString(),
            AppointmentId = encounter.AppointmentId?.ToString() ?? string.Empty,
            EncounterDate = AsUtc(encounter.EncounterDate).ToTimestamp(),
            EncounterTypeCode = encounter.EncounterTypeCode,
            EncounterTypeName = encounter.EncounterTypeName,
            StatusCode = encounter.StatusCode,
            StatusName = encounter.StatusName,
            ChiefComplaint = encounter.ChiefComplaint ?? string.Empty,
            HasVitals = encounter.VitalSigns is not null,
            DiagnosisCount = encounter.Diagnoses.Count,
            CreatedAt = AsUtc(encounter.CreatedAt).ToTimestamp(),
            UpdatedAt = encounter.UpdatedAt is null ? null : AsUtc(encounter.UpdatedAt.Value).ToTimestamp(),
        };

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
