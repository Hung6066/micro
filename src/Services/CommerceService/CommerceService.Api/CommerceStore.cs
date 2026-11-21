using System.Collections.Concurrent;

namespace His.Hope.CommerceService.Api;

public sealed record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    string Description,
    decimal UnitPrice,
    decimal WholesaleUnitPrice,
    int MinOrderQty,
    bool SupportsPrivateLabel,
    bool SupportsExport,
    string TenantKey);

public sealed record ProductCatalogItemDto(
    Guid Id,
    string Sku,
    string Name,
    string Description,
    decimal EffectiveUnitPrice,
    decimal ListUnitPrice,
    decimal WholesaleUnitPrice,
    int MinOrderQty,
    bool SupportsPrivateLabel,
    bool SupportsExport,
    string PriceTier,
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
    string CompanyName,
    string PriceTier);

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
    string CompanyName,
    string? PriceTier);

public sealed record RfqLineDto(Guid ProductId, int Quantity, string? Notes);

public sealed record RfqDto(
    Guid Id,
    string TenantKey,
    string BuyerUserId,
    string Status,
    string Message,
    decimal? QuotedTotal,
    string? OperatorNotes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt,
    IReadOnlyList<RfqLineDto> Lines);

public sealed record CreateRfqRequest(string Message, IReadOnlyList<RfqLineDto> Lines);

public sealed record RespondRfqRequest(decimal QuotedTotal, string OperatorNotes, string Status);

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

    public IReadOnlyList<ProductCatalogItemDto> GetProductsForBuyer(string tenantKey, string? priceTier, IReadOnlyList<ProductDto>? persistedProducts = null)
    {
        var tier = NormalizePriceTier(priceTier);
        var products = persistedProducts ?? _products.Values.ToArray();
        return products
            .Where(product => string.Equals(product.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase))
            .Select(product => ToCatalogItem(product, tier))
            .OrderBy(product => product.Name)
            .ToArray();
    }

    public ProductCatalogItemDto? GetProductForBuyer(
        string tenantKey,
        string? priceTier,
        Guid productId,
        IReadOnlyList<ProductDto>? persistedProducts = null) =>
        GetProductsForBuyer(tenantKey, priceTier, persistedProducts)
            .FirstOrDefault(product => product.Id == productId);

    public IReadOnlyList<ProductDto> GetProducts(string tenantKey, IReadOnlyList<ProductDto>? persistedProducts = null) =>
        (persistedProducts ?? _products.Values.ToArray())
            .Where(product => string.Equals(product.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(product => product.Name)
            .ToArray();

    public IReadOnlyList<ProductDto> GetSeedProducts() => _products.Values.ToArray();

    public void ReplaceProducts(IEnumerable<ProductDto> products)
    {
        _products.Clear();
        foreach (var product in products)
            _products[product.Id] = product;
    }

    public OrderDto? CreateOrder(
        string tenantKey,
        string userId,
        string email,
        CartDto? persistedCart = null,
        string? persistedPriceTier = null,
        IReadOnlyList<ProductDto>? persistedProducts = null)
    {
        var cart = persistedCart ?? GetCart(tenantKey, userId);
        if (cart.Lines.Count == 0)
            return null;

        var priceTier = persistedPriceTier ?? GetProfile(tenantKey, userId, email).PriceTier;
        var lines = new List<OrderLineDto>();
        decimal total = 0;
        var products = (persistedProducts ?? _products.Values.ToArray())
            .ToDictionary(product => product.Id);
        foreach (var line in cart.Lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
                continue;
            if (!string.Equals(product.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase))
                continue;

            var unitPrice = ResolveUnitPrice(product, priceTier);
            var amount = unitPrice * line.Quantity;
            total += amount;
            lines.Add(new OrderLineDto(
                product.Id,
                product.Sku,
                product.Name,
                line.Quantity,
                unitPrice));
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

        return order;
    }

    public NotificationDto CompleteOrder(OrderDto order)
    {
        _carts[CartKey(order.TenantKey, order.BuyerUserId)] = new CartDto(order.TenantKey, []);
        var notification = new NotificationDto(
            Guid.NewGuid(),
            order.TenantKey,
            order.BuyerUserId,
            "Order placed",
            $"Order {order.Id.ToString()[..8]} submitted — total {order.TotalAmount:C}.",
            DateTimeOffset.UtcNow,
            false);
        _notifications[notification.Id] = notification;
        return notification;
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

    private CartDto GetCart(string tenantKey, string userId)
    {
        var key = CartKey(tenantKey, userId);
        return _carts.GetOrAdd(key, _ => new CartDto(tenantKey, []));
    }

    private ProfileDto GetProfile(string tenantKey, string userId, string email)
    {
        var key = ProfileKey(tenantKey, userId);
        return _profiles.GetOrAdd(key, _ => new ProfileDto(
            userId,
            tenantKey,
            email.Split('@')[0],
            email,
            "",
            "",
            "standard"));
    }

    private static string NormalizePriceTier(string? tier) =>
        tier?.Trim().ToLowerInvariant() switch
        {
            "wholesale" => "wholesale",
            "distributor" => "distributor",
            _ => "standard",
        };

    private static decimal ResolveUnitPrice(ProductDto product, string priceTier) =>
        priceTier switch
        {
            "distributor" => product.WholesaleUnitPrice * 0.92m,
            "wholesale" => product.WholesaleUnitPrice,
            _ => product.UnitPrice,
        };

    private static ProductCatalogItemDto ToCatalogItem(ProductDto product, string priceTier) =>
        new(
            product.Id,
            product.Sku,
            product.Name,
            product.Description,
            ResolveUnitPrice(product, priceTier),
            product.UnitPrice,
            product.WholesaleUnitPrice,
            product.MinOrderQty,
            product.SupportsPrivateLabel,
            product.SupportsExport,
            priceTier,
            product.TenantKey);

    private static string CartKey(string tenantKey, string userId) => $"{tenantKey}:{userId}";

    private static string ProfileKey(string tenantKey, string userId) => $"{tenantKey}:{userId}";

    private void SeedProducts()
    {
        var tenant = "customer-factory-x";
        var products = new[]
        {
            new ProductDto(Guid.Parse("11111111-1111-1111-1111-111111111101"), "FX-MANGO-SOFT", "Xoài sấy dẻo", "Xoài sấy dẻo nguyên miếng, chua ngọt cuốn — túi 100g.", 85000m, 72000m, 10, true, true, tenant),
            new ProductDto(Guid.Parse("11111111-1111-1111-1111-111111111102"), "FX-MANGO-CHILI", "Xoài sấy muối ớt", "Xoài sấy muối ớt Đậm vị miền Tây — túi 100g.", 85000m, 72000m, 10, true, true, tenant),
            new ProductDto(Guid.Parse("11111111-1111-1111-1111-111111111103"), "FX-PINE-SOFT", "Thơm sấy dẻo", "Thơm sấy dẻo chua thanh, khoanh tròn dai dai — túi 100g.", 79000m, 67000m, 10, true, true, tenant),
            new ProductDto(Guid.Parse("11111111-1111-1111-1111-111111111104"), "FX-PINE-CHILI", "Thơm sấy muối ớt", "Thơm sấy muối ớt sấy lạnh nguyên vị — túi 100g.", 79000m, 67000m, 10, true, true, tenant),
            new ProductDto(Guid.Parse("11111111-1111-1111-1111-111111111105"), "FX-PASSION", "Chanh dây sấy dẻo", "Chanh dây sấy dẻo hạt giòn, chua ngọt nhai vui miệng.", 89000m, 76000m, 10, true, true, tenant),
            new ProductDto(Guid.Parse("11111111-1111-1111-1111-111111111106"), "FX-MIX", "Trái cây sấy hỗn hợp", "Mix xoài, chanh dây, thơm — phối vị đặc sắc miền Tây.", 95000m, 81000m, 20, true, true, tenant),
            new ProductDto(Guid.Parse("11111111-1111-1111-1111-111111111107"), "FX-KUMQUAT", "Tắc sấy mật ong", "Tắc sấy dẻo mật ong — vị chua ngọt đậm đà.", 92000m, 78000m, 10, true, false, tenant),
            new ProductDto(Guid.Parse("11111111-1111-1111-1111-111111111108"), "FX-RAMBUTAN", "Chôm chôm sấy dẻo", "Chôm chôm sấy dẻo — món lạ từ vườn cây miền Tây.", 98000m, 83000m, 10, true, true, tenant),
        };

        foreach (var product in products)
            _products[product.Id] = product;
    }
}
