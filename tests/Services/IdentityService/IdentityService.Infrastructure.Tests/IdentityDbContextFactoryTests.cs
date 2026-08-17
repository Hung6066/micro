using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class IdentityDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_uses_identity_connection_environment_variable_first()
    {
        const string identity = "Host=identity-test;Port=5432;Database=identity;Username=test;Password=test";
        const string legacy = "Host=legacy-test;Port=5432;Database=legacy;Username=test;Password=test";
        var previousIdentity = Environment.GetEnvironmentVariable("ConnectionStrings__IdentityDb");
        var previousLegacy = Environment.GetEnvironmentVariable("IDENTITY_DB_CONNECTION");

        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__IdentityDb", identity);
            Environment.SetEnvironmentVariable("IDENTITY_DB_CONNECTION", legacy);

            using var context = new IdentityDbContextFactory().CreateDbContext([]);

            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
            Assert.Contains("identity-test", context.Database.GetDbConnection().ConnectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__IdentityDb", previousIdentity);
            Environment.SetEnvironmentVariable("IDENTITY_DB_CONNECTION", previousLegacy);
        }
    }

    [Fact]
    public void CreateDbContext_falls_back_to_legacy_environment_variable()
    {
        const string legacy = "Host=legacy-test;Port=5432;Database=legacy;Username=test;Password=test";
        var previousIdentity = Environment.GetEnvironmentVariable("ConnectionStrings__IdentityDb");
        var previousLegacy = Environment.GetEnvironmentVariable("IDENTITY_DB_CONNECTION");

        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__IdentityDb", null);
            Environment.SetEnvironmentVariable("IDENTITY_DB_CONNECTION", legacy);

            using var context = new IdentityDbContextFactory().CreateDbContext([]);

            Assert.Contains("legacy-test", context.Database.GetDbConnection().ConnectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__IdentityDb", previousIdentity);
            Environment.SetEnvironmentVariable("IDENTITY_DB_CONNECTION", previousLegacy);
        }
    }
}
