using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SystemDashboard.Bff.Services;
using Xunit;

namespace SystemDashboard.Bff.Tests;

public sealed class KubernetesPodMetricsServiceTests
{
    [Fact]
    public async Task GetServiceMetricsAsync_AggregatesPodsByApplicationLabel()
    {
        const string podPayload = """
        {
          "items": [
            {"metadata":{"name":"patient-1","labels":{"app.kubernetes.io/name":"patient-service"}}},
            {"metadata":{"name":"patient-2","labels":{"app.kubernetes.io/name":"patient-service"}}},
            {"metadata":{"name":"identity-1","labels":{"app.kubernetes.io/name":"identity-service"}}}
          ]
        }
        """;
        const string metricsPayload = """
        {
          "items": [
            {"metadata":{"name":"patient-1"},"containers":[{"usage":{"cpu":"25m","memory":"128Mi"}}]},
            {"metadata":{"name":"patient-2"},"containers":[{"usage":{"cpu":"5000000n","memory":"64Mi"}}]},
            {"metadata":{"name":"identity-1"},"containers":[{"usage":{"cpu":"10m","memory":"32Mi"}}]}
          ]
        }
        """;
        using var httpClient = new HttpClient(new JsonHandler(podPayload, metricsPayload))
        {
            BaseAddress = new Uri("https://kubernetes.default.svc")
        };
        var service = new KubernetesPodMetricsService(httpClient, NullLogger<KubernetesPodMetricsService>.Instance);

        var result = await service.GetServiceMetricsAsync(["patient-service", "identity-service"]);

        Assert.Equal(3.0, result["patient-service"].CpuPercent, precision: 6);
        Assert.Equal(192, result["patient-service"].MemoryUsedMb, precision: 6);
        Assert.Equal(1.0, result["identity-service"].CpuPercent, precision: 6);
        Assert.Equal(32, result["identity-service"].MemoryUsedMb, precision: 6);
    }

    private sealed class JsonHandler(string podPayload, string metricsPayload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    request.RequestUri?.AbsolutePath.StartsWith("/api/") == true ? podPayload : metricsPayload,
                    System.Text.Encoding.UTF8,
                    "application/json")
            });
    }
}
