using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Services;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class LdapConfigTests
{
    [Fact]
    public void Validate_RejectsPlaintextLdap()
    {
        var config = new LdapConfig
        {
            Enabled = true,
            Server = "directory.example.test",
            BindDn = "cn=service",
            BindPassword = "secret",
            SearchBase = "dc=example,dc=test",
            UseSsl = false,
            RequireStartTls = false
        };

        config.Validate().Should().ContainSingle(error => error.Contains("plaintext"));
    }

    [Fact]
    public void Validate_RejectsUnresolvedProductionPlaceholders()
    {
        var config = new LdapConfig
        {
            Enabled = true,
            Server = "${LDAP_SERVER}",
            BindDn = "${LDAP_BIND_DN}",
            BindPassword = "${LDAP_BIND_PASSWORD}",
            SearchBase = "${LDAP_SEARCH_BASE}",
            UseSsl = true
        };

        config.Validate().Should().HaveCount(4);
    }

    [Fact]
    public void Validate_AllowsLdapsConfiguration()
    {
        var config = new LdapConfig
        {
            Enabled = true,
            Server = "directory.example.test",
            BindDn = "cn=service",
            BindPassword = "secret",
            SearchBase = "dc=example,dc=test",
            UseSsl = true,
            Port = 636
        };

        config.Validate().Should().BeEmpty();
    }

    [Fact]
    public void Validate_RejectsOutOfRangePort()
    {
        var config = new LdapConfig
        {
            Enabled = true,
            Server = "directory.example.test",
            BindDn = "cn=service",
            BindPassword = "secret",
            SearchBase = "dc=example,dc=test",
            UseSsl = true,
            Port = 70000
        };

        config.Validate().Should().ContainSingle(error => error.Contains("valid TCP port"));
    }

    [Fact]
    public void Validate_AllowsDisabledConfigurationWithoutConnectionSettings()
    {
        new LdapConfig { Enabled = false, Port = 0 }.Validate().Should().BeEmpty();
    }
}
