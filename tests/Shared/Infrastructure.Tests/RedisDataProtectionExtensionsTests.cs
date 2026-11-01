using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using His.Hope.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace His.Hope.Infrastructure.Tests;

[Collection("shared-redis")]
public sealed class RedisDataProtectionExtensionsTests(RedisTestFixture fixture)
{
    [Fact]
    public async Task ProductionServicesUseAnEncryptedServiceScopedRedisKeyRing()
    {
        const string certificatePassword = "data-protection-test-password";
        var certificatePath = Path.Combine(Path.GetTempPath(), $"dp-{Guid.NewGuid():N}.pfx");
        await File.WriteAllBytesAsync(certificatePath, CreateCertificate(certificatePassword));

        try
        {
            const string redisPrefix = "micro:production:";
            var rawDatabase = fixture.Connection.GetDatabase();
            var keyRings = new[]
            {
                (ApplicationName: "His.Hope.CommerceService", RedisKey: $"HisHope:CommerceService:DataProtection:Keys:{Guid.NewGuid():N}"),
                (ApplicationName: "His.Hope.ContentService", RedisKey: $"HisHope:ContentService:DataProtection:Keys:{Guid.NewGuid():N}"),
                (ApplicationName: "His.Hope.ManufacturingService", RedisKey: $"HisHope:ManufacturingService:DataProtection:Keys:{Guid.NewGuid():N}")
            };

            foreach (var (applicationName, redisKey) in keyRings)
            {
                var configuration = CreateConfiguration(certificatePath, certificatePassword, fixture.ConnectionString, redisPrefix);
                var services = new ServiceCollection();
                services.AddHisHopeRedisDataProtection(
                    configuration,
                    CreateEnvironment(Environments.Production),
                    applicationName,
                    redisKey);

                using var provider = services.BuildServiceProvider();
                var dataProtection = provider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();
                var protector = dataProtection.CreateProtector("integration-test");
                var payload = Encoding.UTF8.GetBytes("payload");
                var protectedValue = protector.Protect(payload);

                Assert.Equal(payload, protector.Unprotect(protectedValue));
                Assert.True(await rawDatabase.KeyExistsAsync(redisPrefix + redisKey));
            }
        }
        finally
        {
            File.Delete(certificatePath);
        }
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("missing.pfx", "password")]
    public void ProductionStartupFailsClosedWhenTheCertificateIsMissing(string? certificatePath, string password)
    {
        var configuration = CreateConfiguration(
            certificatePath,
            password,
            fixture.ConnectionString,
            "micro:production:");
        var services = new ServiceCollection();

        var error = Assert.Throws<InvalidOperationException>(() => services.AddHisHopeRedisDataProtection(
            configuration,
            CreateEnvironment(Environments.Production),
            "His.Hope.CommerceService",
            "HisHope:CommerceService:DataProtection:Keys"));

        Assert.Contains("DataProtection certificate", error.Message, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(password))
            Assert.DoesNotContain(password, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionStartupRejectsAnInvalidPfxWithoutLeakingItsPassword()
    {
        const string password = "never-include-this-in-errors";
        var invalidCertificatePath = Path.Combine(Path.GetTempPath(), $"invalid-dp-{Guid.NewGuid():N}.pfx");
        File.WriteAllText(invalidCertificatePath, "not a certificate");

        try
        {
            var configuration = CreateConfiguration(
                invalidCertificatePath,
                password,
                fixture.ConnectionString,
                "micro:production:");
            var services = new ServiceCollection();

            var error = Assert.Throws<InvalidOperationException>(() => services.AddHisHopeRedisDataProtection(
                configuration,
                CreateEnvironment(Environments.Production),
                "His.Hope.CommerceService",
                "HisHope:CommerceService:DataProtection:Keys"));

            Assert.Contains("certificate is invalid", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(password, error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(invalidCertificatePath);
        }
    }

    private static byte[] CreateCertificate(string password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=DataProtection-Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(2));
        return certificate.Export(X509ContentType.Pfx, password);
    }

    private static IConfiguration CreateConfiguration(string? certificatePath, string certificatePassword, string redisConnectionString, string redisPrefix) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = redisConnectionString,
                ["Redis:KeyPrefix"] = redisPrefix,
                ["DataProtection:CertificatePath"] = certificatePath,
                ["DataProtection:CertificatePassword"] = certificatePassword
            })
            .Build();

    private static IHostEnvironment CreateEnvironment(string environmentName) => new TestHostEnvironment
    {
        EnvironmentName = environmentName
    };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Infrastructure.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
