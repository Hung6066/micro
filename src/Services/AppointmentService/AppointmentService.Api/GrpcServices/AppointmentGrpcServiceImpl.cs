using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using His.Hope.AppointmentGrpc;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;

namespace His.Hope.AppointmentService.Api.GrpcServices;

[Authorize]
public class AppointmentGrpcServiceImpl : AppointmentGrpcService.AppointmentGrpcServiceBase
{
    private readonly IAppointmentRepository _repository;

    public AppointmentGrpcServiceImpl(IAppointmentRepository repository) =>
        _repository = repository;

    public override async Task<AppointmentResponse> GetAppointment(AppointmentRequest request,
        ServerCallContext context)
    {
        var apt = await _repository.GetByIdAsync(
            AppointmentId.From(Guid.Parse(request.Id)), context.CancellationToken);

        if (apt is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Appointment not found"));

        return MapToResponse(apt);
    }

    public override async Task<AppointmentListResponse> GetPatientAppointments(
        PatientAppointmentsRequest request, ServerCallContext context)
    {
        var patientId = Guid.Parse(request.PatientId);
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;

        var allAppointments = await _repository.GetByPatientIdAsync(patientId, context.CancellationToken);
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
        var exists = await _repository.ExistsAsync(
            AppointmentId.From(Guid.Parse(request.Id)), context.CancellationToken);

        return new AppointmentExistsResponse { Exists = exists };
    }

    private static AppointmentResponse MapToResponse(Appointment apt) =>
        new()
        {
            Id = apt.Id.ToString()!,
            PatientId = apt.PatientId.ToString(),
            ProviderId = apt.ProviderId.ToString(),
            ScheduledDate = apt.ScheduledDate.ToTimestamp(),
            StartTime = apt.ScheduledDate.Date.Add(apt.StartTime).ToTimestamp(),
            EndTime = apt.ScheduledDate.Date.Add(apt.EndTime).ToTimestamp(),
            StatusCode = apt.Status.Code,
            StatusName = apt.Status.Name,
            TypeCode = apt.Type.Code,
            CreatedAt = apt.CreatedAt.ToTimestamp(),
            UpdatedAt = apt.UpdatedAt?.ToTimestamp()
        };
}
