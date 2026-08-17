using FluentAssertions;
using His.Hope.IdentityService.Api.Composition;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class DpopTokenResponseHandlerTests
{
    [Fact]
    public async Task Required_client_with_access_token_gets_dpop_token_type()
    {
        var handler = new DpopTokenResponseHandler(Configuration("mobile"));
        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest { ClientId = "mobile" },
            Response = new OpenIddictResponse { AccessToken = "access-token" }
        };
        var context = new OpenIddictServerEvents.ApplyTokenResponseContext(transaction);

        await handler.HandleAsync(context);

        transaction.Response.TokenType.Should().Be("DPoP");
    }

    [Fact]
    public async Task Non_required_client_or_missing_token_keeps_default_token_type()
    {
        var handler = new DpopTokenResponseHandler(Configuration("mobile"));
        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest { ClientId = "browser" },
            Response = new OpenIddictResponse { AccessToken = null, TokenType = "Bearer" }
        };
        var context = new OpenIddictServerEvents.ApplyTokenResponseContext(transaction);

        await handler.HandleAsync(context);

        transaction.Response.TokenType.Should().Be("Bearer");
    }

    [Fact]
    public async Task Required_client_with_whitespace_access_token_does_not_advertise_dpop()
    {
        var handler = new DpopTokenResponseHandler(Configuration("mobile"));
        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest { ClientId = "mobile" },
            Response = new OpenIddictResponse { AccessToken = "   ", TokenType = "Bearer" }
        };
        var context = new OpenIddictServerEvents.ApplyTokenResponseContext(transaction);

        await handler.HandleAsync(context);

        transaction.Response.TokenType.Should().Be("Bearer");
    }

    [Fact]
    public async Task Client_id_matching_is_case_sensitive()
    {
        var handler = new DpopTokenResponseHandler(Configuration("mobile"));
        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest { ClientId = "MOBILE" },
            Response = new OpenIddictResponse { AccessToken = "access-token", TokenType = "Bearer" }
        };
        var context = new OpenIddictServerEvents.ApplyTokenResponseContext(transaction);

        await handler.HandleAsync(context);

        transaction.Response.TokenType.Should().Be("Bearer");
    }

    [Fact]
    public async Task Missing_configuration_leaves_access_token_response_unchanged()
    {
        var configuration = new ConfigurationBuilder().Build();
        var handler = new DpopTokenResponseHandler(configuration);
        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest { ClientId = "mobile" },
            Response = new OpenIddictResponse { AccessToken = "access-token", TokenType = "Bearer" }
        };
        var context = new OpenIddictServerEvents.ApplyTokenResponseContext(transaction);

        await handler.HandleAsync(context);

        transaction.Response.TokenType.Should().Be("Bearer");
    }

    [Fact]
    public async Task Required_client_overwrites_existing_token_type_when_access_token_exists()
    {
        var handler = new DpopTokenResponseHandler(Configuration("mobile"));
        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest { ClientId = "mobile" },
            Response = new OpenIddictResponse { AccessToken = "access-token", TokenType = "Bearer" }
        };
        var context = new OpenIddictServerEvents.ApplyTokenResponseContext(transaction);

        await handler.HandleAsync(context);

        transaction.Response.TokenType.Should().Be("DPoP");
    }

    private static IConfiguration Configuration(string clientId) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Dpop:RequiredClientIds:0"] = clientId
        }).Build();
}
