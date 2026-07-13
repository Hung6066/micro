using Grpc.Core;
using His.Hope.AppointmentGrpc;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;

namespace His.Hope.AppointmentService.Api.GrpcServices;

[Authorize]
public class AppointmentGrpcServiceImpl : AppointmentGrpcService.AppointmentGrpcServiceBase
{
    private readonly List<Appointment> _appointments;

    public AppointmentGrpcServiceImpl(List<Appointment> appointments) =>
        _appointments = appointments;

    public override Task<AppointmentResponse> GetAppointment(AppointmentRequest request,
        ServerCallContext context)
    {
        var apt = _appointments.FirstOrDefault(a =>
            a.Id == AppointmentId.From(Guid.Parse(request.Id)));

        if (apt is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Appointment not found"));

        return Task.FromResult(MapToResponse(apt));
    }

    public override Task<AppointmentListResponse> GetPatientAppointments(
        PatientAppointmentsRequest request, ServerCallContext context)
    {
        var patientId = Guid.Parse(request.PatientId);
        var patientAppointments = _appointments
            .Where(a => a.PatientId == patientId)
            .Select(MapToResponse);

        var response = new AppointmentListResponse();
        response.Appointments.AddRange(patientAppointments);
        return Task.FromResult(response);
    }

    public override Task<AppointmentExistsResponse> CheckAppointmentExists(
        AppointmentExistsRequest request, ServerCallContext context)
    {
        var exists = _appointments.Any(a =>
            a.Id == AppointmentId.From(Guid.Parse(request.Id)));

        return Task.FromResult(new AppointmentExistsResponse { Exists = exists });
    }

    private static AppointmentResponse MapToResponse(Appointment apt) =>
        new()
        {
            Id = apt.Id.ToString()!,
            PatientId = apt.PatientId.ToString(),
            ProviderId = apt.ProviderId.ToString(),
            ScheduledDate = apt.ScheduledDate.ToString("O"),
            StartTime = apt.StartTime.ToString(),
            EndTime = apt.EndTime.ToString(),
            StatusCode = apt.Status.Code,
            StatusName = apt.Status.Name,
            TypeCode = apt.Type.Code,
        };
}
