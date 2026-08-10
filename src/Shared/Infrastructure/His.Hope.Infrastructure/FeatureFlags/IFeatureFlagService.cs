namespace His.Hope.Infrastructure.FeatureFlags;

/// <summary>
/// Abstraction for evaluating feature flags with graceful fallback on failure.
/// Supports both Unleash and other feature flag providers behind the same interface.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>
    /// Determines whether the specified feature flag is enabled.
    /// </summary>
    /// <param name="flagName">The name of the feature flag to evaluate.</param>
    /// <param name="defaultValue">The default value to return if the flag cannot be evaluated.</param>
    /// <returns><c>true</c> if the feature is enabled; otherwise, <c>false</c>.</returns>
    Task<bool> IsEnabledAsync(string flagName, bool defaultValue = false);
}
