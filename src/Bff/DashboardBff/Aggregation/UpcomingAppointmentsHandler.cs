using His.Hope.Bff.Core.Aggregation;
using Polly;
using Polly.Registry;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DashboardBff.Aggregation;

public sealed class UpcomingAppointmentsHandler : IAggregationHandler
{
    public string Route => "/api/v1/dashboard/upcoming-appointments";
    public string Method => "GET";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<UpcomingAppointmentsHandler> _logger;

    public UpcomingAppointmentsHandler(
        IHttpClientFactory httpClientFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<UpcomingAppointmentsHandler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _pipeline = pipelineProvider.GetPipeline("bff-downstream");
        _logger = logger;
    }

    public async Task<AggregationResult> HandleAsync(AggregationContext context)
    {
        try
        {
            var items = await _pipeline.ExecuteAsync(async ct =>
            {
                _logger.LogDebug("Fetching upcoming appointments through authorized appointment search");
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    "/api/v1/appointments/search?q=&page=1&pageSize=10");
                if (!string.IsNullOrWhiteSpace(context.SessionJwt))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.SessionJwt);

                var client = _httpClientFactory.CreateClient("appointment-api");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (!document.RootElement.TryGetProperty("items", out var itemElement) ||
                    itemElement.ValueKind != JsonValueKind.Array)
                {
                    return Array.Empty<JsonElement>();
                }

                return itemElement.EnumerateArray().Select(item => item.Clone()).ToArray();
            }, context.CancellationToken).AsTask();

            return AggregationResult.Success(new { items });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch upcoming appointments");
            return AggregationResult.Partial(
                new { items = Array.Empty<object>() },
                new[] { new DegradedField("appointments", "Appointment service unavailable", "unknown") });
        }
    }

}
