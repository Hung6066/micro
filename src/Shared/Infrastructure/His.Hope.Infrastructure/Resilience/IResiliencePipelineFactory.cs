using Polly;

namespace His.Hope.Infrastructure.Resilience;

public interface IResiliencePipelineFactory
{
    ResiliencePipeline GetPipeline(string dependencyName);
    ResiliencePipeline GetGrpcPipeline(string dependencyName);
}
