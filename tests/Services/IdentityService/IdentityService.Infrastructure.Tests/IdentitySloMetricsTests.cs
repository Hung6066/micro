using FluentAssertions;
using His.Hope.IdentityService.Api.Metrics;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class IdentitySloMetricsTests
{
    [Fact]
    public void RecordMethods_AreCallableForAllIdentitySignals()
    {
        IdentitySloMetrics.RecordTokenIssued("password");
        IdentitySloMetrics.RecordTokenFailure("refresh_token", "invalid");
        IdentitySloMetrics.RecordLoginSucceeded("password");
        IdentitySloMetrics.RecordLoginFailed("locked_out");
        IdentitySloMetrics.RecordIntrospection();
        IdentitySloMetrics.RecordTokenRevoked("logout");

        IdentitySloMetrics.TokensIssued.Name.Should().Be("identity.tokens.issued");
    }

    [Fact]
    public void MeasurementScopes_DisposeWithoutThrowing()
    {
        using (IdentitySloMetrics.MeasureTokenIssue()) { }
        using (IdentitySloMetrics.MeasureIntrospection()) { }
        using (IdentitySloMetrics.MeasureLogin()) { }
    }
}
