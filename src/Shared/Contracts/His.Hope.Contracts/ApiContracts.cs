namespace His.Hope.Contracts;

public static class ApiProblemExtensions
{
    public const string CorrelationId = "correlationId";
    public const string ErrorCode = "errorCode";
}

public sealed record ApiProblemResponse(
    string Type,
    string Title,
    int Status,
    string? Detail = null,
    string? Instance = null,
    string? CorrelationId = null,
    string? ErrorCode = null,
    IReadOnlyDictionary<string, string[]>? Errors = null,
    int? RetryAfterSeconds = null);

public static class ApiErrorCodes
{
    public const string Validation = "validation_error";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string UnprocessableEntity = "unprocessable_entity";
    public const string Conflict = "conflict";
    public const string RateLimited = "rate_limited";
    public const string Internal = "internal_error";

    public static string ForStatus(int status) => status switch
    {
        400 => Validation,
        401 => Unauthorized,
        403 => Forbidden,
        404 => NotFound,
        422 => UnprocessableEntity,
        409 => Conflict,
        429 => RateLimited,
        >= 500 => Internal,
        _ => $"http_{status}"
    };
}

public static class ApiConcurrencyHeaders
{
    public const string EntityTag = "ETag";
    public const string IfMatch = "If-Match";
    public const string IfNoneMatch = "If-None-Match";
}

public sealed record ConcurrencyConflict(string Resource, string ResourceId, string? ExpectedVersion, string? ActualVersion);
