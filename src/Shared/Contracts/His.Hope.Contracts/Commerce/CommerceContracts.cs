namespace His.Hope.Contracts.Commerce;

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
