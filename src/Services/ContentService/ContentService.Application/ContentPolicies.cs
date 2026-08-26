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

    public static string? ValidateInquiryStatus(string status) =>
        ContentInquiryStatuses.IsValid(status.Trim().ToLowerInvariant())
            ? null
            : "invalid_status";
}
