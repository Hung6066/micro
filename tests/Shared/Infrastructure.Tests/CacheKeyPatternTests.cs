using His.Hope.Infrastructure.Caching;

namespace His.Hope.Infrastructure.Tests;

public sealed class CacheKeyPatternTests
{
    [Fact]
    public void ForPrefix_MatchesUnpartitionedAndAuthorizationPartitionedKeys()
    {
        var patterns = CacheKeyPattern.ForPrefix("HisHope:", "patients:search:");

        patterns.Should().Contain("HisHope:patients:search:*");
        patterns.Should().Contain("HisHope:authz-cache:*:patients:search:*");
    }

    [Fact]
    public void ForPrefix_RejectsMissingInstancePrefix()
    {
        var action = () => CacheKeyPattern.ForPrefix("", "patients:");

        action.Should().Throw<ArgumentException>();
    }
}
