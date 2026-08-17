using FluentAssertions;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class ExternalIdentityProviderRuntimeTests
{
    [Fact]
    public async Task GetLdapAsync_OverlaysPersistedValuesAndKeepsDefaultsForMalformedValues()
    {
        await using var db = CreateDb();
        db.SystemSettings.AddRange(
            new SystemSetting { Key = "Ldap:Enabled", Value = "true" },
            new SystemSetting { Key = "Ldap:Port", Value = "not-a-port" },
            new SystemSetting { Key = "Ldap:UseSsl", Value = "false" },
            new SystemSetting { Key = "Ldap:GroupRoleMapping", Value = "{malformed" });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ldap:Server"] = "ldap.example.test",
            ["Ldap:Port"] = "636",
            ["Ldap:UseSsl"] = "true",
            ["Ldap:SearchBase"] = "dc=example,dc=test"
        }).Build();

        var result = await new ExternalIdentityProviderRuntime(config, db).GetLdapAsync();

        result.Enabled.Should().BeTrue();
        result.Server.Should().Be("ldap.example.test");
        result.Port.Should().Be(636); // malformed persisted value does not erase the configured default
        result.UseSsl.Should().BeFalse();
        result.SearchBase.Should().Be("dc=example,dc=test");
    }

    [Fact]
    public async Task GetLdapAsync_ReadsValidPersistedGroupRoleMapping()
    {
        await using var db = CreateDb();
        db.SystemSettings.Add(new SystemSetting
        {
            Key = "Ldap:GroupRoleMapping",
            Value = "{\"cn=clinicians\":\"Provider\"}"
        });
        await db.SaveChangesAsync();

        var result = await new ExternalIdentityProviderRuntime(
            new ConfigurationBuilder().Build(), db).GetLdapAsync();

        result.GroupRoleMapping.Should().ContainKey("cn=clinicians")
            .WhoseValue.Should().Be("Provider");
    }

    [Fact]
    public async Task GetSamlAsync_UsesDatabaseOverridesAndNormalizesIssuerPrefix()
    {
        await using var db = CreateDb();
        db.SystemSettings.AddRange(
            new SystemSetting { Key = "Saml2:Enabled", Value = "true" },
            new SystemSetting { Key = "Saml2:Issuer", Value = "Issuer: https://idp.example.test" },
            new SystemSetting { Key = "Saml2:GroupRoleMapping", Value = "{\"staff\":\"Admin\"}" });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Saml2:Issuer"] = "https://fallback.example.test",
            ["Saml2:EmailClaim"] = "mail"
        }).Build();

        var result = await new ExternalIdentityProviderRuntime(config, db).GetSamlAsync();

        result.Enabled.Should().BeTrue();
        result.Issuer.Should().Be("https://idp.example.test");
        result.EmailClaim.Should().Be("mail");
        result.GroupRoleMapping["staff"].Should().Be("Admin");
    }

    [Fact]
    public async Task GetSamlAsync_IgnoresMalformedPersistedMapping()
    {
        await using var db = CreateDb();
        db.SystemSettings.Add(new SystemSetting
        {
            Key = "Saml2:GroupRoleMapping",
            Value = "not-json"
        });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Saml2:GroupRoleMapping:staff"] = "Provider"
        }).Build();

        var result = await new ExternalIdentityProviderRuntime(config, db).GetSamlAsync();

        result.GroupRoleMapping.Should().ContainKey("staff")
            .WhoseValue.Should().Be("Provider");
    }

    private static IdentityDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new IdentityDbContext(options);
    }
}
