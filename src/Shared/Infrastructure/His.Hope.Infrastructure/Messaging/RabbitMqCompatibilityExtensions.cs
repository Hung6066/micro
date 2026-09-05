using His.Hope.Configuration;
using His.Hope.EventBusRabbitMQ.Abstractions;
using His.Hope.EventBusRabbitMQ.Implementations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.Infrastructure.Messaging;

/// <summary>
/// Compatibility registration for consumers that still use the legacy
/// <see cref="His.Hope.EventBus.Abstractions.IEventBus"/> subscription API.
/// New producers should use the Base Service IMessagePublisher pipeline.
/// </summary>
public static class RabbitMqCompatibilityExtensions
{
    public static IServiceCollection AddHisHopeLegacyRabbitMqEventBus(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddRabbitMQEventBus(options =>
        {
            options.HostName = configuration.GetValue(HisHopeConfigurationKeys.EventBus.HostName, "localhost")!;
            options.Port = configuration.GetValue(HisHopeConfigurationKeys.EventBus.Port, 5672);
            options.UserName = configuration.GetValue(HisHopeConfigurationKeys.EventBus.UserName, "admin")!;
            options.Password = EventBusSecurity.GetPassword(configuration);
            options.ExchangeName = configuration.GetValue(
                HisHopeConfigurationKeys.EventBus.InternalExchangeName, "his_hope_exchange")!;
            options.ExternalExchangeName = configuration.GetValue(
                "ExternalIntegration:ExchangeName", "his_hope_external_exchange")!;
            options.PublisherChannelPoolSize = configuration.GetValue(
                HisHopeConfigurationKeys.EventBus.PublisherChannelPoolSize, 8);
            options.PublisherConfirmTimeoutMilliseconds = configuration.GetValue(
                HisHopeConfigurationKeys.EventBus.PublisherConfirmTimeoutMilliseconds, 5000);
            options.UseSsl = configuration.GetValue(HisHopeConfigurationKeys.EventBus.UseSsl, false);
            options.ClientCertificatePath = configuration[HisHopeConfigurationKeys.EventBus.ClientCertificatePath];
            options.ClientCertificatePassword = configuration[HisHopeConfigurationKeys.EventBus.ClientCertificatePassword];
        });
    }
}
