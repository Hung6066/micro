using His.Hope.Bff.Core.Aggregation;
using His.Hope.ClinicalGrpc;
using Polly;
using Polly.Registry;
using Grpc.Core;

namespace DashboardBff.Aggregation;

public sealed class RecentEncountersHandler : IAggregationHandler
{
    public string Route => "/api/v1/dashboard/recent-encounters";
    public string Method => "GET";

    private readonly ClinicalGrpcService.ClinicalGrpcServiceClient _clinicalClient;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<RecentEncountersHandler> _logger;

    public RecentEncountersHandler(
        ClinicalGrpcService.ClinicalGrpcServiceClient clinicalClient,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<RecentEncountersHandler> logger)
    {
        _clinicalClient = clinicalClient;
        _pipeline = pipelineProvider.GetPipeline("bff-downstream");
        _logger = logger;
    }

    public async Task<AggregationResult> HandleAsync(AggregationContext context)
    {
        try
        {
            var headers = CreateHeaders(context.SessionJwt);
            var encounters = await _pipeline.ExecuteAsync(async ct =>
            {
                _logger.LogDebug("Fetching recent encounters");
                var resp = await _clinicalClient.SearchEncountersAsync(
                    new EncounterSearchRequest { Page = 1, PageSize = 10 },
                    headers: headers, cancellationToken: ct);
                return resp.Encounters;
            }, context.CancellationToken).AsTask();

            return AggregationResult.Success(new { encounters });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch recent encounters");
            return AggregationResult.Failed("Clinical service unavailable");
        }
    }

    private static Metadata? CreateHeaders(string jwt) =>
        string.IsNullOrWhiteSpace(jwt)
            ? null
            : new Metadata { { "authorization", $"Bearer {jwt}" } };
}
