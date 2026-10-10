using Grpc.Core;
using His.Hope.Configuration;

namespace His.Hope.Configuration.Tests;

public sealed class ServiceToServiceAuthenticationTests
{
    [Fact]
    public async Task CallCredentials_AddsBearerTokenWhenCallHasNoAuthorization()
    {
        var credentials = new ServiceAuthorizationCallCredentials(new StubTokenProvider("service-token"));
        var metadata = new Metadata();

        await credentials.ApplyAsync(null!, metadata);

        Assert.Equal("Bearer service-token", metadata.GetValue("authorization"));
    }

    [Fact]
    public async Task CallCredentials_DoesNotReplacePropagatedAuthorization()
    {
        var credentials = new ServiceAuthorizationCallCredentials(new StubTokenProvider("service-token"));
        var metadata = new Metadata { { "authorization", "Bearer user-token" } };

        await credentials.ApplyAsync(null!, metadata);

        Assert.Equal("Bearer user-token", metadata.GetValue("authorization"));
    }

    private sealed class StubTokenProvider(string? token) : IServiceAccessTokenProvider
    {
        public ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(token);
    }
}
