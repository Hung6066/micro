using Microsoft.Extensions.Configuration;
using OpenIddict.Server;

namespace His.Hope.IdentityService.Api.Composition;

/// <summary>Advertises the sender-constrained token type to DPoP clients.</summary>
public sealed class DpopTokenResponseHandler(IConfiguration configuration) :
    IOpenIddictServerHandler<OpenIddictServerEvents.ApplyTokenResponseContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ApplyTokenResponseContext>()
            .UseScopedHandler<DpopTokenResponseHandler>()
            .SetOrder(int.MaxValue - 89_000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public ValueTask HandleAsync(OpenIddictServerEvents.ApplyTokenResponseContext context)
    {
        var requiredClients = configuration
            .GetSection("Dpop:RequiredClientIds")
            .Get<string[]>();

        if (requiredClients?.Contains(context.Request.ClientId, StringComparer.Ordinal) == true &&
            !string.IsNullOrWhiteSpace(context.Response.AccessToken))
        {
            context.Response.TokenType = "DPoP";
        }

        return ValueTask.CompletedTask;
    }
}
