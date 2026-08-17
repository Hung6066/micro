using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using His.Hope.Authorization;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace His.Hope.Authorization.Tests;

public sealed class OpenFgaClientTests
{
    [Fact]
    public async Task Check_and_list_objects_use_openfga_contract()
    {
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/check", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { allowed = true }) };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { objects = new[] { "report:1", "report:2" } }) };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://openfga/") };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AUTHZ_OPENFGA_STORE_ID"] = "store",
            ["AUTHZ_OPENFGA_MODEL_ID"] = "model"
        }).Build();
        var client = new OpenFgaClient(http, configuration);

        (await client.CheckAsync("user:1", "viewer", "report:1")).Should().BeTrue();
        (await client.ListObjectsAsync("user:1", "viewer", "report")).Should().BeEquivalentTo(new[] { "report:1", "report:2" });
        handler.Requests.Should().HaveCount(2);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responseFactory(request));
        }
    }
}
