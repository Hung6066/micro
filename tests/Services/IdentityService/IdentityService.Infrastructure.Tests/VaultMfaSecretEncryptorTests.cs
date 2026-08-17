using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class VaultMfaSecretEncryptorTests
{
    [Fact]
    public void Constructor_rejects_missing_vault_address_instead_of_using_local_fallback()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var act = () => new VaultMfaSecretEncryptor(
            configuration,
            NullLogger<VaultMfaSecretEncryptor>.Instance,
            Mock.Of<IVaultTokenProvider>());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Vault is not configured. Use AesMfaSecretEncryptor (DataProtection) for development.");
    }

    [Fact]
    public void Encrypt_posts_base64_plaintext_to_configured_transit_key()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{\"data\":{\"ciphertext\":\"vault:v1:encrypted\"}}"));
        var provider = new Mock<IVaultTokenProvider>();
        provider.Setup(x => x.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("vault-token");
        using var encryptor = CreateEncryptor(handler, provider.Object);

        var result = encryptor.Encrypt("secret-value");

        result.Should().Be(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("vault:v1:encrypted")));
        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.AbsolutePath.Should().Be("/v1/transit/encrypt/custom-mfa");
        handler.Request.Headers.GetValues("X-Vault-Token").Single().Should().Be("vault-token");
        handler.Body.Should().Contain("c2VjcmV0LXZhbHVl");
    }

    [Fact]
    public void Decrypt_decodes_vault_ciphertext_and_returns_plaintext()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            "{\"data\":{\"plaintext\":\"c2VjcmV0LXZhbHVl\"}}"));
        var provider = new Mock<IVaultTokenProvider>();
        provider.Setup(x => x.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("vault-token");
        using var encryptor = CreateEncryptor(handler, provider.Object);

        var result = encryptor.Decrypt(Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("vault:v1:encrypted")));

        result.Should().Be("secret-value");
        handler.Request!.RequestUri!.AbsolutePath.Should().Be("/v1/transit/decrypt/custom-mfa");
        handler.Body.Should().Contain("vault:v1:encrypted");
    }

    [Fact]
    public void Encrypt_rejects_response_with_empty_ciphertext()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{\"data\":{\"ciphertext\":\"\"}}"));
        using var encryptor = CreateEncryptor(handler, Mock.Of<IVaultTokenProvider>());

        var act = () => encryptor.Encrypt("secret-value");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Vault encrypt response missing ciphertext.");
    }

    [Fact]
    public void Encrypt_rejects_response_without_ciphertext_field()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{\"data\":{}}"));
        using var encryptor = CreateEncryptor(handler, Mock.Of<IVaultTokenProvider>());

        var act = () => encryptor.Encrypt("secret-value");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Vault encrypt response missing ciphertext.");
    }

    private static VaultMfaSecretEncryptor CreateEncryptor(
        HttpMessageHandler handler,
        IVaultTokenProvider tokenProvider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Vault:Address"] = "https://vault.test",
                ["Vault:Transit:MfaKeyName"] = "custom-mfa"
            })
            .Build();
        var encryptor = new VaultMfaSecretEncryptor(
            configuration,
            NullLogger<VaultMfaSecretEncryptor>.Instance,
            tokenProvider);
        var field = typeof(VaultMfaSecretEncryptor).GetField(
            "_httpClient", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        field!.SetValue(encryptor, new HttpClient(handler)
        {
            BaseAddress = new Uri("https://vault.test")
        });
        return encryptor;
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            return Task.FromResult(responder(request));
        }
    }
}
