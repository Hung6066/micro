using His.Hope.CommerceService.Application.Orders;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.CommerceService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCommerceApplication(this IServiceCollection services)
    {
        services.AddSingleton<ICommerceOrderPersistence, NoopCommerceOrderPersistence>();
        services.AddSingleton<ICommerceCatalogPersistence, NoopCommerceCatalogPersistence>();
        services.AddSingleton<ICommerceCartPersistence, NoopCommerceCartPersistence>();
        services.AddSingleton<ICommerceProfilePersistence, NoopCommerceProfilePersistence>();
        services.AddSingleton<ICommerceNotificationPersistence, NoopCommerceNotificationPersistence>();
        services.AddSingleton<ICommerceRfqPersistence, NoopCommerceRfqPersistence>();
        return services;
    }
}
