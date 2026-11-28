using His.Hope.IdentityService.Application.Risk;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace His.Hope.IdentityService.Api.Risk;

public static class RiskScoringRegistration
{
    /// <summary>Zero Trust 3.5 R0: report-only sign-in risk scoring (options section <c>RiskScoring</c>).</summary>
    public static IServiceCollection AddSignInRiskScoring(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RiskScoringOptions>()
            .Bind(configuration.GetSection(RiskScoringOptions.SectionName))
            .Validate(options => options.MediumThreshold > 0 && options.HighThreshold > options.MediumThreshold,
                $"{RiskScoringOptions.SectionName}: HighThreshold must be greater than MediumThreshold (> 0).");
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IRiskScorer, RiskScorer>();
        services.AddSingleton<ISignInRiskSignalStore, RedisSignInRiskSignalStore>();
        services.AddSingleton<ISignInRiskRecorder, SignInRiskRecorder>();
        return services;
    }
}
