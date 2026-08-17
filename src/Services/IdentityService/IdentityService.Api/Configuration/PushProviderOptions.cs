using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace His.Hope.IdentityService.Api.Configuration;

public sealed class PushProviderOptions
{
    /// <summary>
    /// Legacy compatibility flag retained for configuration compatibility.
    /// APNs is controlled explicitly by <see cref="ApnsEnabled"/>.
    /// </summary>
    public bool RequireProductionCredentials { get; set; } = true;
    public bool ApnsEnabled { get; set; } = true;
    public string FirebaseCredentialsJson { get; set; } = string.Empty;
    public string FirebaseCredentialsFile { get; set; } = string.Empty;
    public string ApnsKeyId { get; set; } = string.Empty;
    public string ApnsTeamId { get; set; } = string.Empty;
    public string ApnsPrivateKey { get; set; } = string.Empty;
    public string ApnsBundleId { get; set; } = string.Empty;
    public string ApnsEndpoint { get; set; } = "https://api.push.apple.com";

    public IEnumerable<ValidationResult> Validate()
    {
        if (!Uri.TryCreate(ApnsEndpoint, UriKind.Absolute, out var apnsEndpoint) ||
            apnsEndpoint.Scheme != Uri.UriSchemeHttps)
            yield return new ValidationResult("PushProviders:ApnsEndpoint must be an HTTPS URL");
        if (!IsValidFirebaseCredentials(FirebaseCredentialsJson))
            yield return new ValidationResult("PushProviders:FirebaseCredentialsJson is required");
        if (!ApnsEnabled)
            yield break;

        if (!IsConfigured(ApnsKeyId) || !IsConfigured(ApnsTeamId) ||
            !IsConfigured(ApnsPrivateKey) || !IsConfigured(ApnsBundleId))
            yield return new ValidationResult("PushProviders APNs key, team, private key and bundle id are required");
        else
        {
            if (!Regex.IsMatch(ApnsKeyId, "^[A-Za-z0-9]{10}$", RegexOptions.CultureInvariant))
                yield return new ValidationResult("PushProviders:ApnsKeyId must be a 10-character Apple key id");
            if (!Regex.IsMatch(ApnsTeamId, "^[A-Za-z0-9]{10}$", RegexOptions.CultureInvariant))
                yield return new ValidationResult("PushProviders:ApnsTeamId must be a 10-character Apple team id");
            if (!Regex.IsMatch(ApnsBundleId, "^[A-Za-z0-9][A-Za-z0-9.-]*\\.[A-Za-z0-9.-]+$", RegexOptions.CultureInvariant))
                yield return new ValidationResult("PushProviders:ApnsBundleId must be a reverse-DNS bundle id");
            if (!IsValidEcPrivateKey(ApnsPrivateKey))
                yield return new ValidationResult("PushProviders:ApnsPrivateKey must be a valid EC private key");
        }
    }

    private static bool IsConfigured(string value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Contains("${", StringComparison.Ordinal);

    private static bool IsValidFirebaseCredentials(string value)
    {
        if (!IsConfigured(value)) return false;
        try
        {
            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;
            var projectId = root.GetProperty("project_id").GetString();
            var clientEmail = root.GetProperty("client_email").GetString();
            var privateKey = root.GetProperty("private_key").GetString();
            if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(clientEmail) ||
                !IsConfigured(privateKey ?? "") || privateKey is null) return false;

            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKey);
            return rsa.KeySize >= 2048;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidEcPrivateKey(string value)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(value);
            return ecdsa.KeySize >= 256;
        }
        catch
        {
            return false;
        }
    }
}
