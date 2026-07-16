using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using His.Hope.ClinicalGrpc;
using His.Hope.ClinicalService.Application.DTOs;
using His.Hope.ClinicalService.Application.UseCases.Encounters.Queries;
using His.Hope.ClinicalService.Domain.Repositories;
using His.Hope.ClinicalService.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace His.Hope.ClinicalService.Api.GrpcServices;

[Authorize]
public class ClinicalGrpcServiceImpl : ClinicalGrpcService.ClinicalGrpcServiceBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ClinicalGrpcServiceImpl> _logger;

    public ClinicalGrpcServiceImpl(IMediator mediator, ILogger<ClinicalGrpcServiceImpl> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public override async Task<EncounterResponse> GetEncounter(EncounterRequest request,
        ServerCallContext context)
    {
        var encounter = await _mediator.Send(
            new GetEncounterByIdQuery(Guid.Parse(request.Id)),
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
        var result = await _mediator.Send(
            new SearchEncountersQuery(request.PatientId, page, pageSize),
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
        var result = await _mediator.Send(
            new SearchEncountersQuery(request.SearchTerm, page, pageSize),
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
        var encounter = await _mediator.Send(
            new GetEncounterByIdQuery(Guid.Parse(request.Id)),
            context.CancellationToken);

        return new EncounterExistsResponse { Exists = encounter is not null };
    }

    private static EncounterResponse MapToResponse(EncounterDto encounter) =>
        new()
        {
            Id = encounter.Id.ToString(),
            PatientId = encounter.PatientId.ToString(),
            ProviderId = encounter.ProviderId.ToString(),
            AppointmentId = encounter.AppointmentId?.ToString() ?? string.Empty,
            EncounterDate = encounter.EncounterDate.ToTimestamp(),
            EncounterTypeCode = encounter.EncounterTypeCode,
            EncounterTypeName = encounter.EncounterTypeName,
            StatusCode = encounter.StatusCode,
            StatusName = encounter.StatusName,
            ChiefComplaint = encounter.ChiefComplaint ?? string.Empty,
            HasVitals = encounter.VitalSigns is not null,
            DiagnosisCount = encounter.Diagnoses.Count,
            CreatedAt = encounter.CreatedAt.ToTimestamp(),
            UpdatedAt = encounter.UpdatedAt?.ToTimestamp(),
        };
}
