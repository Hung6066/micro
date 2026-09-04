namespace His.Hope.CommerceService.Application.Customer;

public static class CommerceRfqStatusPolicy
{
    public static bool CanTransition(string currentStatus, string requestedStatus)
    {
        var current = Normalize(currentStatus);
        var requested = Normalize(requestedStatus);

        return current switch
        {
            "submitted" => requested is "submitted" or "quoted" or "declined" or "closed",
            "quoted" => requested is "quoted" or "closed",
            "declined" => requested is "declined" or "closed",
            "closed" => requested == "closed",
            _ => false
        };
    }

    public static string Normalize(string status) => status.Trim().ToLowerInvariant();
}
