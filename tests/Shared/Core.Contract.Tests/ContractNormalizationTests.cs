using FluentAssertions;
using His.Hope.Contracts;
using His.Hope.Contracts.Pagination;
using His.Hope.Contracts.Query;
using His.Hope.Contracts.Commerce;
using System.Text.Json;
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
        ApiErrorCodes.ForStatus(400).Should().Be("validation_error");
        ApiErrorCodes.ForStatus(404).Should().Be("not_found");
        ApiErrorCodes.ForStatus(422).Should().Be(ApiErrorCodes.UnprocessableEntity);
        ApiErrorCodes.ForStatus(429).Should().Be("rate_limited");
        ApiErrorCodes.ForStatus(500).Should().Be(ApiErrorCodes.Internal);
        ApiErrorCodes.FacilityScopeDenied.Should().Be("facility_scope_denied");
        ApiErrorCodes.InvalidJson.Should().Be("invalid_json");
        ApiErrorCodes.PasswordResetRejected.Should().Be("password_reset_rejected");
        ApiProblemExtensions.CorrelationId.Should().Be("correlationId");
        ApiProblemExtensions.ErrorCode.Should().Be("errorCode");
    }

    [Fact]
    public void Commerce_order_placed_contract_preserves_event_and_line_identity()
    {
        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var contract = new CommerceOrderPlacedV1(
            eventId,
            SchemaVersion: 1,
            OccurredAt: DateTimeOffset.UtcNow,
            OrderId: orderId,
            TenantKey: "tenant-a",
            BuyerUserId: "buyer-a",
            TotalAmount: 125.50m,
            Lines: [new CommerceOrderLineV1("product-1", "FG-MANGO", 10m, 12.55m)],
            CorrelationId: "corr-1");

        var json = JsonSerializer.Serialize(contract);
        var roundTrip = JsonSerializer.Deserialize<CommerceOrderPlacedV1>(json);

        roundTrip.Should().NotBeNull();
        roundTrip!.EventId.Should().Be(eventId);
        roundTrip.OrderId.Should().Be(orderId);
        roundTrip.Lines.Should().ContainSingle()
            .Which.Sku.Should().Be("FG-MANGO");
        roundTrip.CorrelationId.Should().Be("corr-1");
    }
}
