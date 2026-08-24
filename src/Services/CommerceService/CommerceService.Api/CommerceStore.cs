using System.Collections.Concurrent;

namespace His.Hope.CommerceService.Api;

public sealed record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    string Description,
    decimal UnitPrice,
    string TenantKey);

public sealed record CartLineDto(Guid ProductId, int Quantity);

public sealed record CartDto(string TenantKey, IReadOnlyList<CartLineDto> Lines);

public sealed record OrderLineDto(Guid ProductId, string Sku, string Name, int Quantity, decimal UnitPrice);

public sealed record OrderDto(
    Guid Id,
    string TenantKey,
    string BuyerUserId,
    string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderLineDto> Lines);

public sealed record ProfileDto(
    string UserId,
    string TenantKey,
    string DisplayName,
    string Email,
    string Phone,
    string CompanyName);

public sealed record NotificationDto(
    Guid Id,
    string TenantKey,
    string UserId,
    string Title,
    string Message,
    DateTimeOffset CreatedAt,
    bool IsRead);

public sealed record UpdateCartRequest(IReadOnlyList<CartLineDto> Lines);

public sealed record UpdateProfileRequest(
    string DisplayName,
    string Phone,
    string CompanyName);

public sealed record UpdateOrderStatusRequest(string Status);

public sealed class CommerceStore
{
    private readonly ConcurrentDictionary<Guid, ProductDto> _products = new();
    private readonly ConcurrentDictionary<string, CartDto> _carts = new();
    private readonly ConcurrentDictionary<Guid, OrderDto> _orders = new();
    private readonly ConcurrentDictionary<string, ProfileDto> _profiles = new();
    private readonly ConcurrentDictionary<Guid, NotificationDto> _notifications = new();

    public CommerceStore()
    {
        SeedProducts();
    }

    public IReadOnlyList<ProductDto> GetProducts(string tenantKey) =>
        _products.Values
            .Where(product => string.Equals(product.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(product => product.Name)
            .ToArray();

    public CartDto GetCart(string tenantKey, string userId)
    {
        var key = CartKey(tenantKey, userId);
        return _carts.GetOrAdd(key, _ => new CartDto(tenantKey, []));
    }

    public CartDto UpdateCart(string tenantKey, string userId, IReadOnlyList<CartLineDto> lines)
    {
        var sanitized = lines
            .Where(line => line.Quantity > 0)
            .Select(line => line with { Quantity = Math.Min(line.Quantity, 999) })
            .ToArray();
        var cart = new CartDto(tenantKey, sanitized);
        _carts[CartKey(tenantKey, userId)] = cart;
        return cart;
    }

    public OrderDto? CreateOrder(string tenantKey, string userId)
    {
        var cart = GetCart(tenantKey, userId);
        if (cart.Lines.Count == 0)
            return null;

        var lines = new List<OrderLineDto>();
        decimal total = 0;
        foreach (var line in cart.Lines)
        {
            if (!_products.TryGetValue(line.ProductId, out var product))
                continue;
            if (!string.Equals(product.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase))
                continue;

            var amount = product.UnitPrice * line.Quantity;
            total += amount;
            lines.Add(new OrderLineDto(
                product.Id,
                product.Sku,
                product.Name,
                line.Quantity,
                product.UnitPrice));
        }

        if (lines.Count == 0)
            return null;

        var order = new OrderDto(
            Guid.NewGuid(),
            tenantKey,
            userId,
            "pending",
            total,
            DateTimeOffset.UtcNow,
            lines);

        _orders[order.Id] = order;
        _carts[CartKey(tenantKey, userId)] = new CartDto(tenantKey, []);

        var notification = new NotificationDto(
            Guid.NewGuid(),
            tenantKey,
            userId,
            "Order placed",
            $"Order {order.Id.ToString()[..8]} submitted — total {total:C}.",
            DateTimeOffset.UtcNow,
            false);
        _notifications[notification.Id] = notification;

        return order;
    }

    public IReadOnlyList<OrderDto> GetOrders(string tenantKey, string? buyerUserId = null)
    {
        var query = _orders.Values
            .Where(order => string.Equals(order.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(buyerUserId))
            query = query.Where(order => string.Equals(order.BuyerUserId, buyerUserId, StringComparison.OrdinalIgnoreCase));

        return query.OrderByDescending(order => order.CreatedAt).ToArray();
    }

    public OrderDto? GetOrder(Guid orderId, string tenantKey) =>
        _orders.TryGetValue(orderId, out var order) &&
        string.Equals(order.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase)
            ? order
            : null;

    public OrderDto? UpdateOrderStatus(Guid orderId, string tenantKey, string status)
    {
        if (!_orders.TryGetValue(orderId, out var order))
            return null;
        if (!string.Equals(order.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase))
            return null;

        var normalized = status.Trim().ToLowerInvariant();
        if (normalized is not ("pending" or "confirmed" or "shipped" or "cancelled"))
            return null;

        var updated = order with { Status = normalized };
        _orders[orderId] = updated;

        var notification = new NotificationDto(
            Guid.NewGuid(),
            tenantKey,
            order.BuyerUserId,
            "Order updated",
            $"Order {orderId.ToString()[..8]} is now {normalized}.",
            DateTimeOffset.UtcNow,
            false);
        _notifications[notification.Id] = notification;

        return updated;
    }

    public ProfileDto GetProfile(string tenantKey, string userId, string email)
    {
        var key = ProfileKey(tenantKey, userId);
        return _profiles.GetOrAdd(key, _ => new ProfileDto(
            userId,
            tenantKey,
            email.Split('@')[0],
            email,
            "",
            ""));
    }

    public ProfileDto UpdateProfile(string tenantKey, string userId, string email, UpdateProfileRequest request)
    {
        var existing = GetProfile(tenantKey, userId, email);
        var updated = existing with
        {
            DisplayName = request.DisplayName.Trim(),
            Phone = request.Phone.Trim(),
            CompanyName = request.CompanyName.Trim(),
        };
        _profiles[ProfileKey(tenantKey, userId)] = updated;
        return updated;
    }

    public IReadOnlyList<NotificationDto> GetNotifications(string tenantKey, string userId) =>
        _notifications.Values
            .Where(notification =>
                string.Equals(notification.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(notification.UserId, userId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(notification => notification.CreatedAt)
            .ToArray();

    private static string CartKey(string tenantKey, string userId) => $"{tenantKey}:{userId}";

    private static string ProfileKey(string tenantKey, string userId) => $"{tenantKey}:{userId}";

    private void SeedProducts()
    {
        var tenant = "customer-factory-x";
        var products = new[]
        {
            new ProductDto(Guid.Parse("11111111-1111-1111-1111-111111111101"), "FX-MANGO-SOFT", "Xoài sấy dẻo", "Xoài sấy dẻo nguyên miếng, chua ngọt cuốn — túi 100g.", 85000m, tenant),
            new ProductDto(Guid.Parse("11111111-1111-1111-1111-111111111102"), "FX-MANGO-CHILI", "Xoài sấy muối ớt", "Xoài sấy muối ớt Đậm vị miền Tây — túi 100g.", 85000m, tenant),
            new ProductDto(Guid.Parse("11111111-1111-1111-1111-111111111103"), "FX-PINE-SOFT", "Thơm sấy dẻo", "Thơm sấy dẻo chua thanh, khoanh tròn dai dai — túi 100g.", 79000m, tenant),
            new ProductDto(Guid.Parse("11111111-1111-1111-1111-111111111104"), "FX-PINE-CHILI", "Thơm sấy muối ớt", "Thơm sấy muối ớt sấy lạnh nguyên vị — túi 100g.", 79000m, tenant),
            new ProductDto(Guid.Parse("11111111-1111-1111-1111-111111111105"), "FX-PASSION", "Chanh dây sấy dẻo", "Chanh dây sấy dẻo hạt giòn, chua ngọt nhai vui miệng.", 89000m, tenant),
            new ProductDto(Guid.Parse("11111111-1111-1111-1111-111111111106"), "FX-MIX", "Trái cây sấy hỗn hợp", "Mix xoài, chanh dây, thơm — phối vị đặc sắc miền Tây.", 95000m, tenant),
            new ProductDto(Guid.Parse("11111111-1111-1111-1111-111111111107"), "FX-KUMQUAT", "Tắc sấy mật ong", "Tắc sấy dẻo mật ong — vị chua ngọt đậm đà.", 92000m, tenant),
            new ProductDto(Guid.Parse("11111111-1111-1111-1111-111111111108"), "FX-RAMBUTAN", "Chôm chôm sấy dẻo", "Chôm chôm sấy dẻo — món lạ từ vườn cây miền Tây.", 98000m, tenant),
        };

        foreach (var product in products)
            _products[product.Id] = product;
    }
}
