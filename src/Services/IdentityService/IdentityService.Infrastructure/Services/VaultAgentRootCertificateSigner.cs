using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.IdentityService.Application.InfraAccess;
using His.Hope.Secrets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace His.Hope.IdentityService.Infrastructure.Services;

/// <summary>
/// Signs the agent key through Vault's SSH secrets engine (POST /v1/{mount}/sign/{role}).
/// Uses the shared mTLS "vault" HttpClient and the service's Vault token; neither the token
/// nor the signed certificate is ever logged.
/// </summary>
public sealed class VaultAgentRootCertificateSigner(
    IHttpClientFactory factory,
    IOptionsMonitor<VaultOptions> vaultOptions,
    IOptionsMonitor<AgentRootOptions> agentOptions,
    IVaultTokenProvider tokenProvider,
    ILogger<VaultAgentRootCertificateSigner> logger) : IAgentRootCertificateSigner
{
    public async Task<AgentRootSignedCertificate> SignAsync(
        string publicKey, string principal, TimeSpan ttl, string keyId, CancellationToken ct)
    {
        var options = agentOptions.CurrentValue;
        if (!Uri.TryCreate(vaultOptions.CurrentValue.Address, UriKind.Absolute, out var address))
            throw new AgentRootSigningException("Vault is not configured.");

        var path = $"/v1/{Uri.EscapeDataString(options.VaultMount)}/sign/{Uri.EscapeDataString(options.VaultRole)}";
        var minutes = Math.Max(1, (int)Math.Ceiling(ttl.TotalMinutes));
        try
        {
            var token = await tokenProvider.GetTokenAsync(ct);
            using var client = factory.CreateClient("vault");
            client.BaseAddress = address;
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(new Dictionary<string, string>
                {
                    ["public_key"] = publicKey,
                    ["valid_principals"] = principal,
                    ["ttl"] = minutes.ToString(CultureInfo.InvariantCulture) + "m",
                    ["cert_type"] = "user",
                    ["key_id"] = keyId,
                }),
            };
            request.Headers.Add("X-Vault-Token", token);
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Vault SSH sign on {Mount}/{Role} failed with HTTP {Status}",
                    options.VaultMount, options.VaultRole, (int)response.StatusCode);
                throw new AgentRootSigningException(
                    $"Vault refused to sign the agent key (HTTP {(int)response.StatusCode}).");
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!document.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("signed_key", out var signedKey) ||
                string.IsNullOrWhiteSpace(signedKey.GetString()))
                throw new AgentRootSigningException("Vault returned no signed key.");
            var serial = data.TryGetProperty("serial_number", out var serialValue) ? serialValue.GetString() : null;
            return new AgentRootSignedCertificate(signedKey.GetString()!.Trim(), serial ?? string.Empty);
        }
        catch (AgentRootSigningException)
        {
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            logger.LogWarning("Vault SSH sign on {Mount}/{Role} failed: {ErrorType}",
                options.VaultMount, options.VaultRole, exception.GetType().Name);
            throw new AgentRootSigningException($"Vault could not sign the agent key ({exception.GetType().Name}).", exception);
        }
    }
}
