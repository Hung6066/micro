using System.Security.Cryptography;
using FluentAssertions;
using His.Hope.IdentityService.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class PushProviderOptionsTests
{
    [Fact]
    public void Unconfigured_providers_are_allowed_when_push_is_optional()
    {
        new PushProviderOptions().Validate(allowUnconfigured: true).Should().BeEmpty();
    }

    [Fact]
    public void Optional_push_still_rejects_partial_or_invalid_configuration()
    {
        new PushProviderOptions { FirebaseCredentialsFile = "missing-credentials.json" }
            .Validate(allowUnconfigured: true).Should().NotBeEmpty();
        new PushProviderOptions { FirebaseCredentialsJson = "${FIREBASE_CREDENTIALS_JSON}" }
            .Validate(allowUnconfigured: true).Should().NotBeEmpty();
        new PushProviderOptions { ApnsKeyId = "ABCDEFGHIJ" }
            .Validate(allowUnconfigured: true).Should().NotBeEmpty();
        new PushProviderOptions { FirebaseCredentialsJson = "invalid", FirebaseCredentialsSecretPath = "secret/push" }
            .Validate(allowUnconfigured: true).Should().NotBeEmpty();
        new PushProviderOptions { FirebaseCredentialsSecretPath = "${FIREBASE_SECRET_PATH}" }
            .Validate(allowUnconfigured: true).Should().NotBeEmpty();
    }

    [Fact]
    public void Production_defaults_allow_startup_without_push_credentials()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json"))
            .Build();

        configuration.GetSection("PushProviders").Get<PushProviderOptions>()!
            .Validate(allowUnconfigured: true).Should().BeEmpty();
    }

    [Fact]
    public void Firebase_can_be_configured_before_apns()
    {
        new PushProviderOptions { FirebaseCredentialsJson = ValidFirebaseCredentials() }
            .Validate(allowUnconfigured: true).Should().BeEmpty();
    }

    [Fact]
    public void Disabled_apns_still_requires_valid_https_endpoint_and_firebase_credentials()
    {
        var options = new PushProviderOptions
        {
            ApnsEnabled = false,
            ApnsEndpoint = "http://localhost",
            FirebaseCredentialsJson = ""
        };

        var errors = options.Validate().Select(error => error.ErrorMessage).ToArray();

        errors.Should().Contain(error => error!.Contains("ApnsEndpoint"));
        errors.Should().Contain(error => error!.Contains("FirebaseCredentialsJson"));
        errors.Should().NotContain(error => error!.Contains("APNs key"));
    }

    [Fact]
    public void Enabled_apns_requires_all_credentials()
    {
        var errors = new PushProviderOptions
        {
            FirebaseCredentialsJson = ValidFirebaseCredentials()
        }.Validate().Select(error => error.ErrorMessage).ToArray();

        errors.Should().ContainSingle(error => error!.Contains("APNs key, team, private key and bundle id"));
    }

    [Fact]
    public void Invalid_apns_identifiers_and_key_are_rejected()
    {
        var errors = new PushProviderOptions
        {
            FirebaseCredentialsJson = ValidFirebaseCredentials(),
            ApnsKeyId = "short",
            ApnsTeamId = "invalid-team",
            ApnsBundleId = "not-a-bundle",
            ApnsPrivateKey = "not-a-key"
        }.Validate().Select(error => error.ErrorMessage).ToArray();

        errors.Should().Contain(error => error!.Contains("ApnsKeyId"));
        errors.Should().Contain(error => error!.Contains("ApnsTeamId"));
        errors.Should().Contain(error => error!.Contains("ApnsBundleId"));
        errors.Should().Contain(error => error!.Contains("ApnsPrivateKey"));
    }

    [Fact]
    public void Valid_firebase_and_apns_configuration_has_no_errors()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = new PushProviderOptions
        {
            FirebaseCredentialsJson = ValidFirebaseCredentials(),
            ApnsKeyId = "ABCDEFGHIJ",
            ApnsTeamId = "1234567890",
            ApnsBundleId = "com.hishope.mobile",
            ApnsPrivateKey = ecdsa.ExportECPrivateKeyPem()
        };

        options.Validate().Should().BeEmpty();
    }

    private static string ValidFirebaseCredentials()
    {
        using var rsa = RSA.Create(2048);
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            project_id = "demo",
            client_email = "push@example.test",
            private_key = rsa.ExportRSAPrivateKeyPem()
        });
    }
}
