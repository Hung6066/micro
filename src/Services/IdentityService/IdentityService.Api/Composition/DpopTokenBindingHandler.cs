using System.Security.Claims;
using System.Text.Json;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace His.Hope.IdentityService.Api.Composition;

public sealed class DpopTokenBindingHandler : IOpenIddictServerHandler<OpenIddictServerEvents.HandleTokenRequestContext>
{
    private const string ConfirmationClaim = "cnf";
    private readonly DpopProofValidator _validator;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    public DpopTokenBindingHandler(
        DpopProofValidator validator,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration)
    {
        _validator = validator;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
    }

    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.HandleTokenRequestContext>()
            .UseScopedHandler<DpopTokenBindingHandler>()
            .SetOrder(int.MaxValue - 90_000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public ValueTask HandleAsync(OpenIddictServerEvents.HandleTokenRequestContext context)
    {
        var clientId = context.Request.ClientId;
        if (!IsDpopRequired(clientId))
            return ValueTask.CompletedTask;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            context.Reject(
                OpenIddictConstants.Errors.InvalidRequest,
                "A DPoP-capable HTTP request is required.",
                null);
            return ValueTask.CompletedTask;
        }

        try
        {
            // Validate against the public URI preserved by the API gateway.
            // The configured issuer is not suitable here in mobile development:
            // the emulator reaches the host through 10.0.2.2 while the browser
            // and discovery document use localhost.
            var result = _validator.Validate(httpContext.Request);
            var principal = context.Principal;
            if (principal is null)
            {
                context.Reject(
                    OpenIddictConstants.Errors.InvalidRequest,
                    "A token principal is required for DPoP binding.",
                    null);
                return ValueTask.CompletedTask;
            }

            var existingThumbprint = ReadThumbprint(principal);
            if (existingThumbprint is not null &&
                !string.Equals(existingThumbprint, result.JwkThumbprint, StringComparison.Ordinal))
            {
                context.Reject(
                    OpenIddictConstants.Errors.InvalidGrant,
                    "The DPoP key does not match the existing token binding.",
                    null);
                return ValueTask.CompletedTask;
            }

            var identity = (ClaimsIdentity)principal.Identity!;
            foreach (var claim in identity.FindAll(ConfirmationClaim).ToList())
                identity.RemoveClaim(claim);

            var binding = new Claim(
                ConfirmationClaim,
                JsonSerializer.Serialize(new { jkt = result.JwkThumbprint }));
            binding.SetDestinations(new[] { OpenIddictConstants.Destinations.AccessToken });
            identity.AddClaim(binding);
        }
        catch (DpopValidationException ex)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest, ex.Message, null);
        }

        return ValueTask.CompletedTask;
    }

    private bool IsDpopRequired(string? clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return false;

        var requiredClients = _configuration
            .GetSection("Dpop:RequiredClientIds")
            .Get<string[]>();

        return requiredClients?.Contains(clientId, StringComparer.Ordinal) == true;
    }

    private static string? ReadThumbprint(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(ConfirmationClaim)?.Value;
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.TryGetProperty("jkt", out var jkt)
                ? jkt.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
