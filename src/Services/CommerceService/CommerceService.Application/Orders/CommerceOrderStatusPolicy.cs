namespace His.Hope.CommerceService.Application.Orders;

public static class CommerceOrderStatusPolicy
{
    public static bool CanTransition(string currentStatus, string requestedStatus)
    {
        var current = Normalize(currentStatus);
        var requested = Normalize(requestedStatus);

        return current switch
        {
            "pending" => requested is "pending" or "confirmed" or "cancelled",
            "confirmed" => requested is "confirmed" or "shipped" or "cancelled",
            "shipped" => requested == "shipped",
            "cancelled" => requested == "cancelled",
            _ => false
        };
    }

    public static string Normalize(string status) => status.Trim().ToLowerInvariant();
}
