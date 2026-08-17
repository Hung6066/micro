using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>Whitelisted table analytics and group details. No SQL or user supplied expressions are evaluated.</summary>
public static class TableAnalysisEndpoints
{
    private static readonly IReadOnlyDictionary<string, string> FormulaCatalog = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["user-active-count-v1"] = "Count users grouped by active state",
        ["role-system-count-v1"] = "Count roles grouped by system state",
        ["client-type-count-v1"] = "Count clients grouped by client type",
    };

    public static RouteGroupBuilder MapTableAnalysisEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/tables/analysis/formulas", () => Results.Ok(FormulaCatalog.Select(item => new { id = item.Key, description = item.Value, version = 1 })));
        group.MapPost("/tables/{resource}/analysis", Analyze)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersRead);
        return group;
    }

    private static async Task<IResult> Analyze(string resource, TableAnalysisRequest request, IdentityDbContext db, CancellationToken ct)
    {
        var operation = request.Operation.Trim().ToLowerInvariant();
        if (operation is not ("aggregate" or "pivot" or "formula"))
            return Results.Problem("Unsupported analysis operation.", statusCode: 400);
        if (operation == "formula" && (string.IsNullOrWhiteSpace(request.FormulaId) || !FormulaCatalog.ContainsKey(request.FormulaId)))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["formulaId"] = ["Select a formula from the approved catalog."] });

        var groupBy = request.GroupBy?.Trim();
        if (string.IsNullOrWhiteSpace(groupBy))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["groupBy"] = ["A groupBy field is required."] });

        var normalized = resource.Trim().ToLowerInvariant();
        var limit = Math.Clamp(request.DetailLimit ?? 0, 0, 1000);
        var groups = normalized switch
        {
            "users" when groupBy.Equals("active", StringComparison.OrdinalIgnoreCase) => BuildGroups((await db.Users.AsNoTracking().Select(user => new { Key = user.IsActive ? "true" : "false", user.Id, Label = user.UserName ?? user.Email ?? user.Id.ToString() }).ToListAsync(ct)).Select(item => (item.Key, item.Id.ToString(), item.Label)), limit),
            "roles" when groupBy.Equals("isSystem", StringComparison.OrdinalIgnoreCase) => BuildGroups((await db.Roles.AsNoTracking().Select(role => new { Key = role.IsSystem ? "true" : "false", role.Id, Label = role.Name ?? role.Id.ToString() }).ToListAsync(ct)).Select(item => (item.Key, item.Id.ToString(), item.Label)), limit),
            "clients" when groupBy.Equals("clientType", StringComparison.OrdinalIgnoreCase) => BuildGroups((await db.OpenIddictApplications.AsNoTracking().Select(client => new { Key = client.ClientType ?? "unknown", Id = client.Id ?? string.Empty, Label = client.DisplayName ?? client.ClientId ?? client.Id ?? string.Empty }).ToListAsync(ct)).Select(item => (item.Key, item.Id, item.Label)), limit),
            _ => null
        };

        return groups is null
            ? Results.ValidationProblem(new Dictionary<string, string[]> { ["groupBy"] = [$"The field '{groupBy}' is not allowed for resource '{normalized}'."] })
            : Results.Ok(new { resource = normalized, operation, formulaId = request.FormulaId, groupBy, rows = groups, total = groups.Sum(row => row.Count) });
    }

    private static IReadOnlyList<TableGroupRow> BuildGroups(IEnumerable<(string Key, string Id, string Label)> source, int detailLimit) =>
        source.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new TableGroupRow(group.Key, group.Count(), detailLimit == 0 ? [] : group.Take(detailLimit).Select(item => new TableGroupItem(item.Id, item.Label)).ToArray())).ToArray();

    public sealed record TableAnalysisRequest(string Operation, string? GroupBy, string? Metric = "count", string? FormulaId = null, int? DetailLimit = 0);
    private sealed record TableGroupRow(string Key, int Count, IReadOnlyList<TableGroupItem> Items);
    private sealed record TableGroupItem(string Id, string Label);
}
