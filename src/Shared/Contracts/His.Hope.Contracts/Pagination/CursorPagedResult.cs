namespace His.Hope.Contracts.Pagination;

public sealed record CursorPageRequest(string? Cursor, int PageSize)
{
    public static CursorPageRequest Create(string? cursor = null, int pageSize = PaginationDefaults.DefaultPageSize)
    {
        if (pageSize == 0) pageSize = PaginationDefaults.DefaultPageSize;
        if (pageSize is < 1 or > PaginationDefaults.MaxPageSize)
            throw new ArgumentOutOfRangeException(nameof(pageSize), $"PageSize must be between 1 and {PaginationDefaults.MaxPageSize}.");

        return new CursorPageRequest(string.IsNullOrWhiteSpace(cursor) ? null : cursor.Trim(), pageSize);
    }
}

public sealed class CursorPagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public string? NextCursor { get; }
    public string? PreviousCursor { get; }
    public bool HasNextPage => !string.IsNullOrWhiteSpace(NextCursor);
    public bool HasPreviousPage => !string.IsNullOrWhiteSpace(PreviousCursor);

    public CursorPagedResult(IReadOnlyList<T> items, string? nextCursor = null, string? previousCursor = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = items;
        NextCursor = nextCursor;
        PreviousCursor = previousCursor;
    }
}
