using System.Security.Cryptography;
using System.Text.Json;
using His.Hope.IdentityService.Api.Configuration;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class PushProviderOptionsTests
{
    [Fact]
    public void Validate_RejectsPlaceholdersAndMalformedProviderMaterial()
    {
        var options = new PushProviderOptions
        {
            FirebaseCredentialsJson = "${FIREBASE_CREDENTIALS_JSON}",
            ApnsKeyId = "short",
            ApnsTeamId = "short",
            ApnsPrivateKey = "not-a-key",
            ApnsBundleId = "not-a-bundle"
        };

        Assert.NotEmpty(options.Validate());
    }

    [Fact]
    public void Validate_AcceptsValidFirebaseRsaAndApnsEcMaterial()
    {
        using var firebaseRsa = RSA.Create(2048);
        using var apnsEc = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = new PushProviderOptions
        {
            FirebaseCredentialsJson = JsonSerializer.Serialize(new
            {
                project_id = "his-hope-test",
                client_email = "firebase-admin@his-hope-test.iam.gserviceaccount.com",
                private_key = firebaseRsa.ExportPkcs8PrivateKeyPem()
            }),
            ApnsKeyId = "ABC1234567",
            ApnsTeamId = "TEAM123456",
            ApnsPrivateKey = apnsEc.ExportPkcs8PrivateKeyPem(),
            ApnsBundleId = "vn.his.hope.mobile"
        };

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void Validate_DoesNotAllowTheProductionBypassFlagToSkipChecks()
    {
        var options = new PushProviderOptions
        {
            RequireProductionCredentials = false
        };

        Assert.NotEmpty(options.Validate());
    }

    [Fact]
    public void Validate_AllowsExplicitlyDisabledApnsWithoutPlaceholderMaterial()
    {
        using var firebaseRsa = RSA.Create(2048);
        var options = new PushProviderOptions
        {
            FirebaseCredentialsJson = JsonSerializer.Serialize(new
            {
                project_id = "his-hope-test",
                client_email = "firebase-admin@his-hope-test.iam.gserviceaccount.com",
                private_key = firebaseRsa.ExportPkcs8PrivateKeyPem()
            }),
            ApnsEnabled = false
        };

        Assert.Empty(options.Validate());
    }
}
