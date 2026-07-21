using FluentAssertions;
using His.Hope.AgentHarness.Application.Services;

namespace His.Hope.AgentHarness.UnitTests.Services;

public class EmbeddingServiceTests
{
    [Fact]
    public void GenerateEmbedding_ShouldReturnNormalizedHarnessVector()
    {
        var service = new EmbeddingService();

        var embedding = service.GenerateEmbedding("error CS0246 missing type");

        embedding.Should().HaveCount(256);
        service.CosineSimilarity(embedding, embedding).Should().BeApproximately(1.0, 0.0001);
    }
}
