using His.Hope.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace Infrastructure.Tests;

public sealed class OpenshipDatabaseMtlsTests
{
    [Fact]
    public void ManagedDatabaseUrlPreservesClientIdentity()
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DATABASE_URL"] = "postgresql://user:password@postgres-ha.openship.internal/micro_identity?sslmode=verify-full",
            ["OPENSHIP_DATABASE_CA_PEM"] = "test-ca",
            ["PLATFORM_PKI_FILE_PG_CLIENT_CRT"] = "test-client-certificate",
            ["PLATFORM_PKI_FILE_PG_CLIENT_KEY"] = "test-client-key"
        });
        configuration.ApplyOpenshipBindings("IdentityDb");
        var value = new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("IdentityDb"));
        Assert.Equal(SslMode.VerifyFull, value.SslMode);
        Assert.Equal("postgres-ha.openship.internal", value.Host);
        Assert.Equal("test-client-certificate", File.ReadAllText(value.SslCertificate));
        Assert.Equal("test-client-key", File.ReadAllText(value.SslKey));
    }

    [Fact]
    public void RejectsPartialClientIdentity()
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DATABASE_URL"] = "postgresql://user:password@postgres-ha.openship.internal/micro_identity?sslmode=verify-full",
            ["OPENSHIP_DATABASE_CA_PEM"] = "test-ca",
            ["PLATFORM_PKI_FILE_PG_CLIENT_CRT"] = "test-client-certificate"
        });
        Assert.Throws<InvalidOperationException>(() => configuration.ApplyOpenshipBindings("IdentityDb"));
    }
}
