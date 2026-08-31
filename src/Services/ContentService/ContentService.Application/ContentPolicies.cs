using His.Hope.ContentService.Domain;

namespace His.Hope.ContentService.Application;

public static class ContentPolicies
{
    public static string? ValidatePartnershipInquiry(CreatePartnershipInquiryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyName) ||
            string.IsNullOrWhiteSpace(request.ContactName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            !request.Email.Contains('@'))
            return "validation_failed";

        return null;
    }

    public static string? ValidateNewsletterEmail(string email) =>
        string.IsNullOrWhiteSpace(email) || !email.Contains('@')
            ? "validation_failed"
            : null;

    public static string? ValidateArticleStatus(string status) =>
        ContentArticleStatuses.IsValid(status.Trim().ToLowerInvariant())
            ? null
            : "invalid_status";

    public static bool CanTransitionArticleStatus(string currentStatus, string requestedStatus)
    {
        var current = currentStatus.Trim().ToLowerInvariant();
        var requested = requestedStatus.Trim().ToLowerInvariant();
        return current switch
        {
            ContentArticleStatuses.Draft => requested is ContentArticleStatuses.Draft or ContentArticleStatuses.Published,
            ContentArticleStatuses.Published => requested is ContentArticleStatuses.Published or ContentArticleStatuses.Archived,
            ContentArticleStatuses.Archived => requested == ContentArticleStatuses.Archived,
            _ => false,
        };
    }

    public static string? ValidateInquiryStatus(string status) =>
        ContentInquiryStatuses.IsValid(status.Trim().ToLowerInvariant())
            ? null
            : "invalid_status";
}
