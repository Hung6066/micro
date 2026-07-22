using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemDashboard.Bff.Authorization;
using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;

namespace SystemDashboard.Bff.Controllers;

[ApiController]
[Route("api/slo")]
[Authorize(Roles = DashboardRoles.ReadOnly)]
public sealed class SloController : ControllerBase
{
    private static readonly Dictionary<string, string> ServiceDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["identity-service"] = "Identity Service",
        ["patient-service"] = "Patient Service",
        ["appointment-service"] = "Appointment Service",
        ["clinical-service"] = "Clinical Service",
        ["lab-service"] = "Lab Service",
        ["billing-service"] = "Billing Service",
        ["pharmacy-service"] = "Pharmacy Service",
        ["api-gateway"] = "API Gateway",
        ["system-dashboard-bff"] = "Dashboard BFF",
    };

    // Prometheus recording rule metric names used by the His.Hope SLO framework
    private const string MetricAvailability = "slo:availability:ratio";
    private const string MetricErrorBudget = "slo:error_budget:remaining";
    private const string MetricBurnRate1h = "slo:burn_rate:1h";
    private const string MetricBurnRate6h = "slo:burn_rate:6h";
    private const string MetricLatencyP99 = "slo:latency:p99";

    private readonly IPrometheusQueryService _prometheus;
    private readonly ILogger<SloController> _logger;

    public SloController(IPrometheusQueryService prometheus, ILogger<SloController> logger)
    {
        _prometheus = prometheus;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetSlo(CancellationToken ct)
    {
        // Fetch all SLO metrics from Prometheus recording rules in parallel
        var availabilityTask = _prometheus.QuerySamplesAsync(MetricAvailability, ct);
        var errorBudgetTask = _prometheus.QuerySamplesAsync(MetricErrorBudget, ct);
        var burnRate1hTask = _prometheus.QuerySamplesAsync(MetricBurnRate1h, ct);
        var burnRate6hTask = _prometheus.QuerySamplesAsync(MetricBurnRate6h, ct);
        var latencyTask = _prometheus.QuerySamplesAsync(MetricLatencyP99, ct);

        // Fetch 24h of latency data for sparkline rendering
        var latencyRangeTask = _prometheus.QueryRangeAsync(
            MetricLatencyP99,
            DateTime.UtcNow.AddHours(-24),
            DateTime.UtcNow,
            "5m",
            ct);

        await Task.WhenAll(
            availabilityTask, errorBudgetTask, burnRate1hTask,
            burnRate6hTask, latencyTask, latencyRangeTask);

        var availabilitySamples = availabilityTask.Result;
        var errorBudgetSamples = errorBudgetTask.Result;
        var burnRate1hSamples = burnRate1hTask.Result;
        var burnRate6hSamples = burnRate6hTask.Result;
        var latencySamples = latencyTask.Result;
        var latencyRangeData = latencyRangeTask.Result;

        // Collect all unique service names from all metric labels
        var serviceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void CollectServices(List<PrometheusSample> samples)
        {
            foreach (var s in samples)
            {
                if (s.Labels.TryGetValue("service", out var svc) && !string.IsNullOrWhiteSpace(svc))
                    serviceNames.Add(svc);
            }
        }

        CollectServices(availabilitySamples);
        CollectServices(errorBudgetSamples);
        CollectServices(burnRate1hSamples);
        CollectServices(burnRate6hSamples);
        CollectServices(latencySamples);

        // Build helper: get value for a specific service from a sample list
        static double GetValue(List<PrometheusSample> samples, string service)
        {
            return samples
                .Where(s => s.Labels.TryGetValue("service", out var svc)
                            && string.Equals(svc, service, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Value)
                .FirstOrDefault();
        }

        // Build the response
        var records = new List<SloRecord>(serviceNames.Count);
        foreach (var svc in serviceNames.OrderBy(s => s))
        {
            var displayName = ServiceDisplayNames.GetValueOrDefault(svc, svc);
            records.Add(new SloRecord
            {
                Service = svc,
                DisplayName = displayName,
                Availability = GetValue(availabilitySamples, svc),
                ErrorBudgetRemaining = GetValue(errorBudgetSamples, svc),
                BurnRate1h = GetValue(burnRate1hSamples, svc),
                BurnRate6h = GetValue(burnRate6hSamples, svc),
                LatencyP99 = GetValue(latencySamples, svc),
            });
        }

        _logger.LogInformation("Returned SLO data for {Count} services", records.Count);
        return Ok(new SloResponse
        {
            Services = records,
            SparklineData = latencyRangeData,
        });
    }
}
