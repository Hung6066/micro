using His.Hope.CommerceService.Application.Orders;

namespace His.Hope.CommerceService.Application.Customer;

public interface ICommerceCustomerWorkflow
{
    Task<CommerceCartSnapshot> GetCartAsync(string tenantKey, string userId, CancellationToken cancellationToken = default);

    Task<CommerceCartSnapshot> UpdateCartAsync(
        string tenantKey,
        string userId,
        IReadOnlyList<CommerceCartLineSnapshot> lines,
        CancellationToken cancellationToken = default);

    Task<CommerceProfileSnapshot> GetProfileAsync(
        string tenantKey,
        string userId,
        string fallbackEmail,
        CancellationToken cancellationToken = default);

    Task<CommerceProfileSnapshot> UpdateProfileAsync(
        string tenantKey,
        string userId,
        string fallbackEmail,
        string displayName,
        string phone,
        string companyName,
        string? priceTier,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommerceNotificationSnapshot>> GetNotificationsAsync(
        string tenantKey,
        string userId,
        CancellationToken cancellationToken = default);

    Task MarkNotificationAsReadAsync(
        Guid notificationId,
        string tenantKey,
        string userId,
        CancellationToken cancellationToken = default);
}

public sealed class CommerceCustomerWorkflow(
    ICommerceCartPersistence cartPersistence,
    ICommerceProfilePersistence profilePersistence,
    ICommerceNotificationPersistence notificationPersistence) : ICommerceCustomerWorkflow
{
    public Task<CommerceCartSnapshot> GetCartAsync(string tenantKey, string userId, CancellationToken cancellationToken = default) =>
        cartPersistence.GetCartAsync(tenantKey, userId, cancellationToken);

    public async Task<CommerceCartSnapshot> UpdateCartAsync(
        string tenantKey,
        string userId,
        IReadOnlyList<CommerceCartLineSnapshot> lines,
        CancellationToken cancellationToken = default)
    {
        var sanitized = lines
            .Where(line => line.Quantity > 0)
            .Select(line => line with { Quantity = Math.Min(line.Quantity, 999) })
            .ToArray();
        var cart = new CommerceCartSnapshot(tenantKey, userId, sanitized);
        await cartPersistence.SaveCartAsync(cart, cancellationToken);
        return cart;
    }

    public Task<CommerceProfileSnapshot> GetProfileAsync(
        string tenantKey,
        string userId,
        string fallbackEmail,
        CancellationToken cancellationToken = default) =>
        profilePersistence.GetProfileAsync(tenantKey, userId, fallbackEmail, cancellationToken);

    public async Task<CommerceProfileSnapshot> UpdateProfileAsync(
        string tenantKey,
        string userId,
        string fallbackEmail,
        string displayName,
        string phone,
        string companyName,
        string? priceTier,
        CancellationToken cancellationToken = default)
    {
        var existing = await profilePersistence.GetProfileAsync(tenantKey, userId, fallbackEmail, cancellationToken);
        var profile = existing with
        {
            DisplayName = displayName.Trim(),
            Phone = phone.Trim(),
            CompanyName = companyName.Trim(),
            PriceTier = string.IsNullOrWhiteSpace(priceTier) ? existing.PriceTier : NormalizePriceTier(priceTier),
        };
        await profilePersistence.SaveProfileAsync(profile, cancellationToken);
        return profile;
    }

    public Task<IReadOnlyList<CommerceNotificationSnapshot>> GetNotificationsAsync(
        string tenantKey,
        string userId,
        CancellationToken cancellationToken = default) =>
        notificationPersistence.GetNotificationsAsync(tenantKey, userId, cancellationToken);

    public Task MarkNotificationAsReadAsync(
        Guid notificationId,
        string tenantKey,
        string userId,
        CancellationToken cancellationToken = default) =>
        notificationPersistence.MarkAsReadAsync(notificationId, tenantKey, userId, cancellationToken);

    private static string NormalizePriceTier(string? tier) => tier?.Trim().ToLowerInvariant() switch
    {
        "wholesale" => "wholesale",
        "distributor" => "distributor",
        _ => "standard",
    };
}
