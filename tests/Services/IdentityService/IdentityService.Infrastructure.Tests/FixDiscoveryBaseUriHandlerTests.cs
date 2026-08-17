using FluentAssertions;
using His.Hope.IdentityService.Api.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using OpenIddict.Server;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class FixDiscoveryBaseUriHandlerTests
{
    [Fact]
    public async Task Null_context_is_rejected()
    {
        var handler = new FixDiscoveryBaseUriHandler(new ConfigurationBuilder().Build());

        var act = () => handler.HandleAsync(null!).AsTask();

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Transaction_without_http_request_is_left_unchanged()
    {
        var handler = new FixDiscoveryBaseUriHandler(new ConfigurationBuilder().Build());
        var context = new OpenIddictServerEvents.HandleConfigurationRequestContext(
            new OpenIddictServerTransaction());

        await handler.HandleAsync(context);

        context.BaseUri.Should().BeNull();
    }

    [Fact]
    public async Task Forwarded_headers_define_public_base_uri()
    {
        var handler = new FixDiscoveryBaseUriHandler(new ConfigurationBuilder().Build());
        var request = new DefaultHttpContext().Request;
        request.Scheme = "http";
        request.PathBase = "/identity";
        request.Headers["X-Forwarded-Host"] = "public.example.test:8443";
        request.Headers["X-Forwarded-Proto"] = "https";
        var transaction = TransactionWithRequest(request);
        var context = new OpenIddictServerEvents.HandleConfigurationRequestContext(transaction);

        await handler.HandleAsync(context);

        context.BaseUri.Should().Be(new Uri("https://public.example.test:8443/identity"));
    }

    [Fact]
    public async Task Issuer_is_used_when_forwarded_host_is_missing()
    {
        var handler = new FixDiscoveryBaseUriHandler(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenIddict:Issuer"] = "https://issuer.example.test"
            }).Build());
        var request = new DefaultHttpContext().Request;
        request.PathBase = "/identity";
        var context = new OpenIddictServerEvents.HandleConfigurationRequestContext(TransactionWithRequest(request));

        await handler.HandleAsync(context);

        context.BaseUri.Should().Be(new Uri("https://issuer.example.test/identity"));
    }

    private static OpenIddictServerTransaction TransactionWithRequest(HttpRequest request)
    {
        var transaction = new OpenIddictServerTransaction();
        transaction.Properties[typeof(HttpRequest).FullName!] = new WeakReference<HttpRequest>(request);
        return transaction;
    }
}
