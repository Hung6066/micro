using His.Hope.Bff.Core.Proxy;
using Xunit;

namespace His.Hope.Bff.Core.Tests.Proxy;

public class JwtTransformProviderTests
{
    [Fact]
    public void JwtTransformProvider_CanBeConstructed()
    {
        var provider = new JwtTransformProvider();
        Assert.NotNull(provider);
    }

    [Fact]
    public void ValidateRoute_DoesNotThrow()
    {
        var provider = new JwtTransformProvider();
        var exception = Record.Exception(() =>
            provider.ValidateRoute(null!));
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateCluster_DoesNotThrow()
    {
        var provider = new JwtTransformProvider();
        var exception = Record.Exception(() =>
            provider.ValidateCluster(null!));
        Assert.Null(exception);
    }
}
