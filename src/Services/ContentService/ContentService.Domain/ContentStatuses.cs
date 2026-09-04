namespace His.Hope.ContentService.Domain;

public static class ContentArticleStatuses
{
    public const string Draft = "draft";
    public const string InReview = "in_review";
    public const string Published = "published";
    public const string Archived = "archived";

    public static bool IsValid(string? status) =>
        status is Draft or InReview or Published or Archived;
}

public static class ContentInquiryStatuses
{
    public const string New = "new";
    public const string Reviewing = "reviewing";
    public const string Contacted = "contacted";
    public const string Closed = "closed";

    public static bool IsValid(string? status) =>
        status is New or Reviewing or Contacted or Closed;
}
