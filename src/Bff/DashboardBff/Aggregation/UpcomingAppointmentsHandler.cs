using His.Hope.Bff.Core.Aggregation;
using His.Hope.AppointmentGrpc;
using Polly;
using Polly.Registry;

namespace DashboardBff.Aggregation;

public sealed class UpcomingAppointmentsHandler : IAggregationHandler
{
    public string Route => "/api/v1/dashboard/upcoming-appointments";
    public string Method => "GET";

    private readonly AppointmentGrpcService.AppointmentGrpcServiceClient _appointmentClient;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<UpcomingAppointmentsHandler> _logger;

    public UpcomingAppointmentsHandler(
        AppointmentGrpcService.AppointmentGrpcServiceClient appointmentClient,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<UpcomingAppointmentsHandler> logger)
    {
        _appointmentClient = appointmentClient;
        _pipeline = pipelineProvider.GetPipeline("bff-downstream");
        _logger = logger;
    }

    public async Task<AggregationResult> HandleAsync(AggregationContext context)
    {
        try
        {
            var appointments = await _pipeline.ExecuteAsync(async ct =>
            {
                _logger.LogDebug("Fetching upcoming appointments");
                var resp = await _appointmentClient.GetPatientAppointmentsAsync(
                    new PatientAppointmentsRequest { Page = 1, PageSize = 10 },
                    cancellationToken: ct);
                return resp.Appointments;
            }, context.CancellationToken).AsTask();

            return AggregationResult.Success(new { appointments });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch upcoming appointments");
            return AggregationResult.Failed("Appointment service unavailable");
        }
    }
}
