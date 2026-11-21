using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using His.Hope.Observability.OpenTelemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

namespace His.Hope.Infrastructure.Tests;

[Collection("OpenTelemetry")]
public sealed class OtlpTlsConfigurationTests
{
    [Theory]
    [InlineData("OTEL_EXPORTER_OTLP_CERTIFICATE")]
    [InlineData("OpenTelemetry:CaCertificatePath")]
    [InlineData("Otlp:CaCertificatePath")]
    public void CaAliases_ConfigureExporterHttpClient(string key)
    {
        using var certificates = new TestCertificates();
        var directory = Path.Combine(Directory.GetCurrentDirectory(), $".tracing-ca-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "ca.pem");
            File.WriteAllText(path, certificates.Root.ExportCertificatePem());
            var configuration = Config(new()
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "https://collector.example:4317",
                [key] = path
            });
            var services = new ServiceCollection();
            services.AddHisHopeOpenTelemetryExporters(configuration, "identity-service");
            using var provider = services.BuildServiceProvider();
            _ = provider.GetRequiredService<TracerProvider>();
            var factory = OtlpTlsConfiguration.ReadHttpClientFactory(configuration, new Uri("https://collector.example:4317"));
            Assert.NotNull(factory);
            using var client = factory();
            Assert.NotNull(client);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void UnconfiguredEndpoint_DoesNotReadCaOrEnableExport()
    {
        Assert.Null(OtlpTlsConfiguration.ReadHttpClientFactory(Config(new()
        {
            ["OTEL_EXPORTER_OTLP_CERTIFICATE"] = "missing-ca.pem"
        }), null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("missing-ca.pem")]
    public void MissingCa_FailsClearly(string path)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            OtlpTlsConfiguration.ReadHttpClientFactory(Config(new()
            {
                ["OTEL_EXPORTER_OTLP_CERTIFICATE"] = path
            }), new Uri("https://collector.example:4317")));
        Assert.Contains("OTEL_EXPORTER_OTLP_CERTIFICATE", exception.Message);
    }

    [Fact]
    public void CaWithHttpEndpoint_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            OtlpTlsConfiguration.ReadHttpClientFactory(Config(new()
            {
                ["OTEL_EXPORTER_OTLP_CERTIFICATE"] = "ca.pem"
            }), new Uri("http://collector.example:4317")));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("invalid")]
    [InlineData("leaf-only")]
    public void InvalidOrNonCaBundle_FailsAtRegistration(string kind)
    {
        using var certificates = new TestCertificates();
        var directory = Path.Combine(Directory.GetCurrentDirectory(), $".tracing-ca-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "ca.pem");
            File.WriteAllText(path, kind switch
            {
                "empty" => "",
                "invalid" => "-----BEGIN CERTIFICATE-----\ninvalid\n-----END CERTIFICATE-----",
                _ => certificates.Leaf.ExportCertificatePem()
            });
            var exception = Assert.Throws<InvalidOperationException>(() =>
                OtlpTlsConfiguration.ReadHttpClientFactory(Config(new()
                {
                    ["OTEL_EXPORTER_OTLP_CERTIFICATE"] = path
                }), new Uri("https://collector.example:4317")));
            Assert.Contains("OTEL_EXPORTER_OTLP_CERTIFICATE", exception.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(SslPolicyErrors.None, true)]
    [InlineData(SslPolicyErrors.RemoteCertificateChainErrors, true)]
    [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch, false)]
    [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors, false)]
    [InlineData(SslPolicyErrors.RemoteCertificateNotAvailable, false)]
    public void PrivateCa_ValidatesChainButNeverBypassesHostname(SslPolicyErrors errors, bool expected)
    {
        using var certificates = new TestCertificates();
        var bundle = new X509Certificate2Collection(certificates.Root);
        Assert.Equal(expected, OtlpTlsConfiguration.ValidateServerCertificate(bundle, certificates.Leaf, null, errors));
    }

    [Theory]
    [InlineData(SslPolicyErrors.None)]
    [InlineData(SslPolicyErrors.RemoteCertificateChainErrors)]
    public void UntrustedServer_IsRejectedEvenIfOperatingSystemTrustsIt(SslPolicyErrors errors)
    {
        using var trusted = new TestCertificates();
        using var untrusted = new TestCertificates();
        Assert.False(OtlpTlsConfiguration.ValidateServerCertificate(
            new(trusted.Root), untrusted.Leaf, null, errors));
    }

    [Fact]
    public void ExpiredServer_IsRejected()
    {
        using var certificates = new TestCertificates(expired: true);
        Assert.False(OtlpTlsConfiguration.ValidateServerCertificate(
            new(certificates.Root), certificates.Leaf, null, SslPolicyErrors.RemoteCertificateChainErrors));
    }

    [Fact]
    public void ClientOnlyCertificate_IsRejectedAsServer()
    {
        using var certificates = new TestCertificates(clientOnly: true);
        Assert.False(OtlpTlsConfiguration.ValidateServerCertificate(
            new(certificates.Root), certificates.Leaf, null, SslPolicyErrors.RemoteCertificateChainErrors));
    }

    [Fact]
    public void MissingServerCertificate_IsRejected()
    {
        using var certificates = new TestCertificates();
        Assert.False(OtlpTlsConfiguration.ValidateServerCertificate(
            new(certificates.Root), null, null, SslPolicyErrors.None));
    }

    [Fact]
    public void CaBundleAndPresentedChain_CanSupplyIntermediatesWithoutTrustingThemAsRoots()
    {
        using var certificates = new TestCertificates(withIntermediate: true);
        Assert.False(OtlpTlsConfiguration.ValidateServerCertificate(
            new(certificates.Root), certificates.Leaf, null, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.True(OtlpTlsConfiguration.ValidateServerCertificate(
            new() { certificates.Root, certificates.Intermediate! }, certificates.Leaf, null, SslPolicyErrors.RemoteCertificateChainErrors));

        using var presented = new X509Chain();
        presented.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        presented.ChainPolicy.DisableCertificateDownloads = true;
        presented.ChainPolicy.ExtraStore.Add(certificates.Intermediate!);
        presented.ChainPolicy.ExtraStore.Add(certificates.Root);
        _ = presented.Build(certificates.Leaf);
        Assert.True(OtlpTlsConfiguration.ValidateServerCertificate(
            new(certificates.Root), certificates.Leaf, presented, SslPolicyErrors.RemoteCertificateChainErrors));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public async Task Exporter_UsesPrivateCaTlsAndValidatesRealServerHostname(bool wrongRoot, bool wrongName, bool expected)
    {
        using var certificates = new TestCertificates(includeLoopbackName: !wrongName);
        using var unrelated = new TestCertificates();
        using var diagnostics = new ExportDiagnostics();
        var directory = Path.Combine(Directory.GetCurrentDirectory(), $".tracing-ca-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options =>
                options.Listen(IPAddress.Loopback, 0, listen =>
                {
                    listen.Protocols = HttpProtocols.Http2;
                    listen.UseHttps(certificates.Leaf);
                }));
            await using var collector = builder.Build();
            var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            collector.MapPost("/opentelemetry.proto.collector.trace.v1.TraceService/Export", async context =>
            {
                using var body = new MemoryStream();
                await context.Request.Body.CopyToAsync(body);
                received.TrySetResult((int)body.Length);
                context.Response.ContentType = "application/grpc";
                context.Response.DeclareTrailer("grpc-status");
                await context.Response.Body.WriteAsync(new byte[5]);
                context.Response.AppendTrailer("grpc-status", "0");
            });
            await collector.StartAsync();
            var endpoint = collector.Urls.Single();
            var path = Path.Combine(directory, "ca.pem");
            File.WriteAllText(path, (wrongRoot ? unrelated.Root : certificates.Root).ExportCertificatePem());
            var services = new ServiceCollection();
            services.AddHisHopeOpenTelemetryExporters(Config(new()
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint,
                ["OTEL_EXPORTER_OTLP_CERTIFICATE"] = path,
            }), "identity-service");
            services.Configure<OtlpExporterOptions>(options => options.TimeoutMilliseconds = 1000);
            using var provider = services.BuildServiceProvider();
            var tracer = provider.GetRequiredService<TracerProvider>();
            using var source = new ActivitySource("identity-service");
            source.StartActivity("private-ca-export")!.Dispose();
            Assert.True(tracer.ForceFlush(5000));

            if (expected)
            {
                Assert.True(received.Task.IsCompleted, string.Join("\n", diagnostics.Messages));
                Assert.True(await received.Task > 0);
            }
            else
                Assert.False(received.Task.IsCompleted);

            await collector.StopAsync();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private sealed class ExportDiagnostics : EventListener
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.Contains("OpenTelemetry"))
                EnableEvents(eventSource, EventLevel.Verbose);
        }
        protected override void OnEventWritten(EventWrittenEventArgs eventData) =>
            Messages?.Enqueue(eventData.EventName + ": " + string.Join(" ", eventData.Payload?.Select(value => value?.ToString()) ?? []));
    }

    private sealed class TestCertificates : IDisposable
    {
        public X509Certificate2 Root { get; }
        public X509Certificate2? Intermediate { get; }
        public X509Certificate2 Leaf { get; }

        public TestCertificates(bool expired = false, bool clientOnly = false, bool includeLoopbackName = true, bool withIntermediate = false)
        {
            var now = DateTimeOffset.UtcNow;
            using var rootKey = RSA.Create(2048);
            var root = new CertificateRequest("CN=Tracing Test Root", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            root.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            root.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
            Root = root.CreateSelfSigned(now.AddDays(-10), now.AddDays(10));
            if (withIntermediate)
            {
                using var intermediateKey = RSA.Create(2048);
                var intermediate = new CertificateRequest("CN=Tracing Test Intermediate", intermediateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                intermediate.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
                intermediate.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
                using var issuedIntermediate = intermediate.Create(Root, now.AddDays(-5), now.AddDays(5), RandomNumberGenerator.GetBytes(16));
                Intermediate = issuedIntermediate.CopyWithPrivateKey(intermediateKey);
            }

            using var leafKey = RSA.Create(2048);
            var leaf = new CertificateRequest("CN=collector.example", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            leaf.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            leaf.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            leaf.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new(clientOnly ? "1.3.6.1.5.5.7.3.2" : "1.3.6.1.5.5.7.3.1") }, true));
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName("collector.example");
            if (includeLoopbackName)
                names.AddIpAddress(IPAddress.Loopback);
            leaf.CertificateExtensions.Add(names.Build());
            using var issued = leaf.Create(Intermediate ?? Root, now.AddDays(-2), expired ? now.AddDays(-1) : now.AddDays(1), RandomNumberGenerator.GetBytes(16));
            using var withKey = issued.CopyWithPrivateKey(leafKey);
            // Schannel cannot serve TLS using CopyWithPrivateKey's ephemeral key.
            Leaf = new X509Certificate2(withKey.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
        }

        public void Dispose()
        {
            Leaf.Dispose();
            Intermediate?.Dispose();
            Root.Dispose();
        }
    }
}
