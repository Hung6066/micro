using His.Hope.Configuration;
using Microsoft.Extensions.Configuration;

namespace His.Hope.Infrastructure.Messaging;

/// <summary>
/// Resolves the RabbitMQ credential without silently falling back to a
/// well-known password when configuration wiring is incomplete.
/// </summary>
public static class EventBusSecurity
{
    public static string GetPassword(IConfiguration configuration)
    {
        var password = configuration[HisHopeConfigurationKeys.EventBus.Password];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "EventBus:Password must be supplied by the runtime secret provider.");

        var environment = configuration["HIS_HOPE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"];
        if (environment is "staging" or "production" &&
            string.Equals(password, "admin", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "EventBus:Password must not use the development default in staging or production.");
        }

        return password;
    }
}
