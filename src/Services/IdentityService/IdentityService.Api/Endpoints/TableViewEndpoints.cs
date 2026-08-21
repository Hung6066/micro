using System.Security.Claims;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.Contracts;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class TableViewEndpoints
{
    public static RouteGroupBuilder MapTableViewEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/tables/{resource}/views", List);
        group.MapPut("/tables/{resource}/views/{name}", Save);
        group.MapDelete("/tables/{resource}/views/{name}", Delete);
        return group;
    }

    internal static bool TryNormalizeResource(string value, out string normalized) =>
        TryNormalize(value, out normalized);

    private static Guid? Subject(HttpContext context) =>
        Guid.TryParse(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub"),
            out var id)
            ? id
            : null;

    private static async Task<IResult> List(string resource, IdentityDbContext db, HttpContext http, CancellationToken ct)
    {
        var denied = await AdminTableResourceAuthorization.AuthorizeTableResourceAsync(http, resource, write: false, ct);
        if (denied is not null) return denied;

        var userId = Subject(http);
        if (userId is null) return Results.Unauthorized();
        if (!TryNormalize(resource, out var normalized))
            return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.InvalidViewResource });
        var views = await db.TableViews.AsNoTracking()
            .Where(view => view.UserId == userId && view.Resource == normalized)
            .OrderBy(view => view.Name)
            .Select(view => new { view.Name, view.PayloadJson, view.UpdatedAt })
            .ToListAsync(ct);
        return Results.Ok(views);
    }

    private static async Task<IResult> Save(string resource, string name, TableViewRequest request, IdentityDbContext db, HttpContext http, CancellationToken ct)
    {
        var denied = await AdminTableResourceAuthorization.AuthorizeTableResourceAsync(http, resource, write: true, ct);
        if (denied is not null) return denied;

        var userId = Subject(http);
        if (userId is null) return Results.Unauthorized();
        if (!TryNormalize(resource, out var normalizedResource) || !TryNormalize(name, out var normalizedName))
            return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.InvalidViewIdentifier });
        if (string.IsNullOrWhiteSpace(request.PayloadJson) || request.PayloadJson.Length > 65536)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["payloadJson"] = ["View payload must be between 1 and 65536 characters."] });
        try { using var _ = System.Text.Json.JsonDocument.Parse(request.PayloadJson); }
        catch (System.Text.Json.JsonException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["payloadJson"] = ["View payload must be valid JSON."] });
        }
        var view = await db.TableViews.SingleOrDefaultAsync(item => item.UserId == userId && item.Resource == normalizedResource && item.Name == normalizedName, ct);
        if (view is null)
        {
            view = new TableView { Id = Guid.NewGuid(), UserId = userId.Value, Resource = normalizedResource, Name = normalizedName };
            db.TableViews.Add(view);
        }
        view.PayloadJson = request.PayloadJson;
        view.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { view.Name, view.PayloadJson, view.UpdatedAt });
    }

    private static async Task<IResult> Delete(string resource, string name, IdentityDbContext db, HttpContext http, CancellationToken ct)
    {
        var denied = await AdminTableResourceAuthorization.AuthorizeTableResourceAsync(http, resource, write: true, ct);
        if (denied is not null) return denied;

        var userId = Subject(http);
        if (userId is null) return Results.Unauthorized();
        if (!TryNormalize(resource, out var normalizedResource) || !TryNormalize(name, out var normalizedName))
            return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.InvalidViewIdentifier });
        var view = await db.TableViews.SingleOrDefaultAsync(item => item.UserId == userId && item.Resource == normalizedResource && item.Name == normalizedName, ct);
        if (view is null) return Results.NotFound();
        db.TableViews.Remove(view);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static bool TryNormalize(string value, out string normalized)
    {
        normalized = value.Trim().ToLowerInvariant();
        return normalized.Length is > 0 and <= 80 &&
               normalized.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
    }

    public sealed record TableViewRequest(string PayloadJson);
}
