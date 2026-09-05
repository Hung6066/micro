using Microsoft.EntityFrameworkCore;

namespace His.Hope.Persistence.Querying;

public static class HisHopePaginationDefaults
{
    public const int FirstPage = 1;
    public const int DefaultPageSize = 50;
    public const int QualityDefaultPageSize = 25;
    public const int ExportDefaultPageSize = 500;
    public const int SmallDefaultPageSize = 100;
    public const int MaxPageSize = 200;
    public const int SmallMaxPageSize = 100;
    public const int ExportMaxPageSize = 5000;
}

public readonly record struct HisHopePage(int Number, int Size)
{
    public static HisHopePage Create(
        int number,
        int size,
        int maxSize = HisHopePaginationDefaults.MaxPageSize) =>
        new(Math.Max(HisHopePaginationDefaults.FirstPage, number), Math.Clamp(size, 1, maxSize));

    public int Skip => (int)Math.Min(int.MaxValue, (long)(Number - 1) * Size);
}

public static class HisHopeQueryExtensions
{
    public static IQueryable<T> ApplyPage<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        int maxPageSize = HisHopePaginationDefaults.MaxPageSize)
    {
        var normalized = HisHopePage.Create(page, pageSize, maxPageSize);
        return query.Skip(normalized.Skip).Take(normalized.Size);
    }

    public static IQueryable<T> TagUseCase<T>(this IQueryable<T> query, string useCase) =>
        query.TagWith($"HisHope.UseCase:{useCase}");
}
