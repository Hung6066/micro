using FluentAssertions;
using His.Hope.Contracts;
using His.Hope.Contracts.Pagination;
using His.Hope.Contracts.Query;
using Xunit;

namespace His.Hope.Core.Contract.Tests;

public sealed class ContractNormalizationTests
{
    [Fact]
    public void Query_request_normalizes_multi_sort_and_filters()
    {
        var request = new QueryRequest(
            Page: 2,
            PageSize: 25,
            Search: "  alice ",
            Sort: "createdAt:desc,username:asc",
            Filters: new Dictionary<string, string?>
            {
                ["role"] = "admin",
                ["isActive"] = "true"
            });

        var normalized = request.Normalize(
            new HashSet<string>(["createdAt", "username"]),
            new HashSet<string>(["role", "isActive"]));

        normalized.Search.Should().Be("alice");
        normalized.SortTerms.Should().BeEquivalentTo(
            [new SortTerm("createdAt", SortDirection.Desc), new SortTerm("username", SortDirection.Asc)]);
        normalized.Filters.Should().BeEquivalentTo(request.Filters);
    }

    [Fact]
    public void Cursor_page_request_applies_defaults_and_bounds_page_size()
    {
        var request = CursorPageRequest.Create(" next ", 0);

        request.Cursor.Should().Be("next");
        request.PageSize.Should().Be(PaginationDefaults.DefaultPageSize);
        Action invalid = () => CursorPageRequest.Create(null, PaginationDefaults.MaxPageSize + 1);
        invalid.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Paged_result_exposes_consistent_page_metadata()
    {
        var result = new PagedResult<string>(["a"], totalCount: 21, page: 2, pageSize: 10);

        result.TotalPages.Should().Be(3);
        result.HasPreviousPage.Should().BeTrue();
        result.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void Api_error_codes_are_stable_for_problem_details()
    {
        ApiErrorCodes.ForStatus(422).Should().Be(ApiErrorCodes.UnprocessableEntity);
        ApiErrorCodes.ForStatus(500).Should().Be(ApiErrorCodes.Internal);
        ApiProblemExtensions.CorrelationId.Should().Be("correlationId");
        ApiProblemExtensions.ErrorCode.Should().Be("errorCode");
    }
}
