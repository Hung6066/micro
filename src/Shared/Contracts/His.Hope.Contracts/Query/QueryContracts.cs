using His.Hope.Contracts.Pagination;

namespace His.Hope.Contracts.Query;

public enum SortDirection { Asc, Desc }

public sealed record SortTerm(string Field, SortDirection Direction);

public sealed record QueryRequest(
    int Page = PaginationDefaults.DefaultPage,
    int PageSize = PaginationDefaults.DefaultPageSize,
    string? Search = null,
    string? Sort = null,
    string? Cursor = null,
    IReadOnlyDictionary<string, string?>? Filters = null)
{
    public IReadOnlyList<SortTerm> SortTerms { get; init; } = [];

    public QueryRequest Validate()
    {
        if (Page < 1) throw new ArgumentOutOfRangeException(nameof(Page), "Page must be at least 1.");
        if (Page > PaginationDefaults.MaxPageNumber)
            throw new ArgumentOutOfRangeException(nameof(Page), $"Page must be at most {PaginationDefaults.MaxPageNumber}; use cursor pagination for deep navigation.");
        if (PageSize is < 1 or > PaginationDefaults.MaxPageSize)
            throw new ArgumentOutOfRangeException(nameof(PageSize), $"PageSize must be between 1 and {PaginationDefaults.MaxPageSize}.");
        return this;
    }

    public QueryRequest Normalize(IReadOnlySet<string> allowedSortFields, IReadOnlySet<string> allowedFilterFields)
    {
        ArgumentNullException.ThrowIfNull(allowedSortFields);
        ArgumentNullException.ThrowIfNull(allowedFilterFields);
        return Validate() with
        {
            Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
            SortTerms = SortContract.Parse(Sort, allowedSortFields),
            Filters = FilterContract.Validate(Filters, allowedFilterFields)
        };
    }
}

public static class SortContract
{
    public static IReadOnlyList<SortTerm> Parse(string? value, IReadOnlySet<string> allowedFields)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        var terms = new List<SortTerm>();
        foreach (var raw in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = raw.Split(':', 2, StringSplitOptions.TrimEntries);
            var field = parts[0];
            if (!allowedFields.Contains(field)) throw new ArgumentException($"Sorting by '{field}' is not supported.", nameof(value));
            var direction = parts.Length == 1 || string.Equals(parts[1], "asc", StringComparison.OrdinalIgnoreCase)
                ? SortDirection.Asc
                : string.Equals(parts[1], "desc", StringComparison.OrdinalIgnoreCase)
                    ? SortDirection.Desc
                    : throw new ArgumentException($"Sort direction for '{field}' must be asc or desc.", nameof(value));
            terms.Add(new SortTerm(field, direction));
        }
        return terms;
    }
}

public static class FilterContract
{
    public static IReadOnlyDictionary<string, string?> Validate(IReadOnlyDictionary<string, string?>? filters, IReadOnlySet<string> allowedFields)
    {
        if (filters is null || filters.Count == 0) return new Dictionary<string, string?>();
        foreach (var key in filters.Keys)
            if (!allowedFields.Contains(key)) throw new ArgumentException($"Filtering by '{key}' is not supported.", nameof(filters));
        return filters;
    }
}

public static class QueryContractAdapter
{
    public static QueryRequest FromGrpc(int page, int pageSize, string? search = null, string? sort = null, string? cursor = null, IReadOnlyDictionary<string, string?>? filters = null) =>
        new QueryRequest(page <= 0 ? PaginationDefaults.DefaultPage : page, pageSize <= 0 ? PaginationDefaults.DefaultPageSize : pageSize, search, sort, cursor, filters).Validate();
}
