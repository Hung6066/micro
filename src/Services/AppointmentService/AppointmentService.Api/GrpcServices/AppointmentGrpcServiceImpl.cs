using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using His.Hope.AppointmentGrpc;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Domain.ValueObjects;
using His.Hope.AppointmentService.Infrastructure.Persistence;
using His.Hope.Authorization;
using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace His.Hope.AppointmentService.Api.GrpcServices;

[Authorize(Policy = AuthorizationPolicyNames.Permissions.AppointmentsView)]
public class AppointmentGrpcServiceImpl : AppointmentGrpcService.AppointmentGrpcServiceBase
{
    private readonly IAppointmentRepository _repository;
    private readonly AppointmentDbContext _db;
    private readonly IResourceAuthorizationEvaluator _authorization;

    public AppointmentGrpcServiceImpl(
        IAppointmentRepository repository,
        AppointmentDbContext db,
        IResourceAuthorizationEvaluator authorization)
    {
        _repository = repository;
        _db = db;
        _authorization = authorization;
    }

    public override async Task<AppointmentResponse> GetAppointment(AppointmentRequest request,
        ServerCallContext context)
    {
        var appointmentId = ParseGuidOrThrow(request.Id, "Appointment id");
        await EnsureResourceAccessAsync(appointmentId, context);
        var apt = await _repository.GetByIdAsync(
            AppointmentId.From(appointmentId), context.CancellationToken);

        if (apt is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Appointment not found"));

        return MapToResponse(apt);
    }

    public override async Task<AppointmentListResponse> GetPatientAppointments(
        PatientAppointmentsRequest request, ServerCallContext context)
    {
        var patientId = ParseGuidOrThrow(request.PatientId, "Patient id");
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;
        var accessScope = FacilityAccessScope.FromPrincipal(context.GetHttpContext().User);

        var allAppointments = await _repository.GetByPatientIdAsync(
            patientId, accessScope.FacilityIds, accessScope.IsCrossFacility, context.CancellationToken);
        var totalCount = allAppointments.Count;
        var paged = allAppointments
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToResponse);

        var response = new AppointmentListResponse();
        response.Appointments.AddRange(paged);
        response.TotalCount = totalCount;
        response.Page = page;
        response.PageSize = pageSize;
        return response;
    }

    public override async Task<AppointmentExistsResponse> CheckAppointmentExists(
        AppointmentExistsRequest request, ServerCallContext context)
    {
        var appointmentId = ParseGuidOrThrow(request.Id, "Appointment id");
        await EnsureResourceAccessAsync(appointmentId, context);
        var exists = await _repository.ExistsAsync(
            AppointmentId.From(appointmentId), context.CancellationToken);

        return new AppointmentExistsResponse { Exists = exists };
    }

    private async Task EnsureResourceAccessAsync(Guid appointmentId, ServerCallContext context)
    {
        var decision = await _authorization.EvaluateResourceAsync(
            _db.Appointments,
            appointment => appointment.Id == AppointmentId.From(appointmentId),
            appointment => appointment.FacilityId,
            context.GetHttpContext().User,
            HisHopePermissions.Appointments.View,
            "appointment",
            appointmentId.ToString("D"),
            context.CancellationToken);
        if (!decision.Allowed)
            throw new RpcException(new Status(StatusCode.NotFound, "Appointment not found"));
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

    private static AppointmentResponse MapToResponse(Appointment apt) =>
        new()
        {
            Id = apt.Id.ToString()!,
            PatientId = apt.PatientId.ToString(),
            ProviderId = apt.ProviderId.ToString(),
            ScheduledDate = AsUtc(apt.ScheduledDate).ToTimestamp(),
            StartTime = AsUtc(apt.ScheduledDate.Date.Add(apt.StartTime)).ToTimestamp(),
            EndTime = AsUtc(apt.ScheduledDate.Date.Add(apt.EndTime)).ToTimestamp(),
            StatusCode = apt.Status.Code,
            StatusName = apt.Status.Name,
            TypeCode = apt.Type.Code,
            CreatedAt = AsUtc(apt.CreatedAt).ToTimestamp(),
            UpdatedAt = apt.UpdatedAt is { } updatedAt ? AsUtc(updatedAt).ToTimestamp() : null
        };
}
