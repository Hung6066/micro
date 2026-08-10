using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SystemDashboard.Bff.Services;
using Xunit;

namespace SystemDashboard.Bff.Tests;

public sealed class LokiQueryServiceTests
{
    [Fact]
    public async Task QueryLogsAsync_MapsLokiStreamsToLogEntries()
    {
        var handler = new StubHandler("""
        {
          "status": "success",
          "data": {
            "resultType": "streams",
            "result": [{
              "stream": {"filename":"/var/log/pods/his-hope-dev_patient-abc_uid/patient-service/0.log","level":"error","service_name":"patient-service"},
              "values": [["1720000000000000000", "request failed traceId=abc"]]
            }]
          }
        }
        """);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://loki") };
        var service = new LokiQueryService(
            client,
            Options.Create(new LokiOptions { Url = "http://loki", DefaultQuery = "{job=\"kubernetes-pods\"}" }),
            NullLogger<LokiQueryService>.Instance);

        var logs = await service.QueryLogsAsync(service: "patient-service", size: 10);

        var log = Assert.Single(logs);
        Assert.Equal("patient-service", log.Service);
        Assert.Equal("error", log.Level);
        Assert.Equal("request failed traceId=abc", log.Message);
        Assert.Equal("abc", log.TraceId);
        Assert.Contains("query=", handler.RequestUri!.Query);
        Assert.Contains("his-hope-dev_patient-service-", Uri.UnescapeDataString(handler.RequestUri.Query));
    }

    [Fact]
    public async Task QueryLogsAsync_UsesContainerWhenPromtailServiceLabelIsGeneric()
    {
        var handler = new StubHandler("""
        {"status":"success","data":{"resultType":"streams","result":[{"stream":{"filename":"/var/log/pods/his-hope-dev_patient-abc_uid/patient-service/0.log","service_name":"kubernetes-pods"},"values":[["1720000000000000000","patient event"]]}]}}
        """);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://loki") };
        var service = new LokiQueryService(
            client,
            Options.Create(new LokiOptions { Url = "http://loki", DefaultQuery = "{job=\"kubernetes-pods\"}" }),
            NullLogger<LokiQueryService>.Instance);

        var logs = await service.QueryLogsAsync(service: "patient-service");

        Assert.Single(logs);
        Assert.Equal("patient-service", logs[0].Service);
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
