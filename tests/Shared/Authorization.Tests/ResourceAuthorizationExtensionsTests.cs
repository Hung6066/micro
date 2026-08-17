using System.Security.Claims;
using FluentAssertions;
using His.Hope.Authorization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace His.Hope.Authorization.Tests;

public sealed class ResourceAuthorizationExtensionsTests
{
    [Fact]
    public async Task Denies_cross_facility_resource_loaded_from_database()
    {
        await using var db = CreateContext(new ResourceRow { Id = "patient-1", FacilityId = "facility-b" });
        var evaluator = new AuthorizationEvaluator();

        var decision = await evaluator.EvaluateResourceAsync(
            db.Resources,
            row => row.Id == "patient-1",
            row => row.FacilityId,
            Principal("patients.view", "facility-a"),
            "patients.view",
            "patient",
            "patient-1");

        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("facility_scope_denied");
    }

    [Fact]
    public async Task Denies_unknown_resource_without_querying_request_facility()
    {
        await using var db = CreateContext();
        var evaluator = new AuthorizationEvaluator();

        var decision = await evaluator.EvaluateResourceAsync(
            db.Resources,
            row => row.Id == "patient-missing",
            row => row.FacilityId,
            Principal("patients.view", "facility-a"),
            "patients.view",
            "patient",
            "patient-missing");

        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("resource_not_found");
    }

    [Fact]
    public async Task Allows_matching_facility_resource()
    {
        await using var db = CreateContext(new ResourceRow { Id = "patient-1", FacilityId = "facility-a" });
        var evaluator = new AuthorizationEvaluator();

        var decision = await evaluator.EvaluateResourceAsync(
            db.Resources,
            row => row.Id == "patient-1",
            row => row.FacilityId,
            Principal("patients.view", "facility-a"),
            "patients.view",
            "patient",
            "patient-1");

        decision.Allowed.Should().BeTrue();
    }

    private static ResourceDbContext CreateContext(params ResourceRow[] rows)
    {
        var options = new DbContextOptionsBuilder<ResourceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var context = new ResourceDbContext(options);
        context.Resources.AddRange(rows);
        context.SaveChanges();
        return context;
    }

    private static ClaimsPrincipal Principal(string permission, string facility) => new(new ClaimsIdentity(
        [new Claim("sub", "user-1"), new Claim("permissions", permission), new Claim("facility_id", facility)],
        "test"));

    private sealed class ResourceDbContext(DbContextOptions<ResourceDbContext> options) : DbContext(options)
    {
        public DbSet<ResourceRow> Resources => Set<ResourceRow>();
    }

    private sealed class ResourceRow
    {
        public string Id { get; set; } = string.Empty;
        public string? FacilityId { get; set; }
    }
}
