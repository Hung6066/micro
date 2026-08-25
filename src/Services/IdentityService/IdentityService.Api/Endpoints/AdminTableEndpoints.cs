using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using His.Hope.Infrastructure.Audit;
using Microsoft.AspNetCore.Authorization;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddictEntityFrameworkCore = OpenIddict.EntityFrameworkCore.Models;
using His.Hope.Contracts.Bulk;
using His.Hope.Contracts;
using His.Hope.Authorization;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Api.Jobs;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Infrastructure.Facility;
using His.Hope.SharedKernel.Authorization;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class AdminTableEndpoints
{
    public static RouteGroupBuilder MapAdminTableEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/tables/users/bulk", BulkUsers)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite)
            .WithTenantMutationScope();
        group.MapPost("/tables/roles/bulk", BulkRoles)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();
        group.MapPost("/tables/clients/bulk", BulkClients)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminClientsWrite)
            .WithTenantMutationScope();
        group.MapPost("/tables/{resource}/export", Export)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersRead)
            .WithTenantReadScope(HisHopePermissions.Reports.Export);
        group.MapGet("/tables/jobs/{jobId}", GetJob)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersRead);
        group.MapGet("/tables/jobs/{jobId}/events", StreamJob)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersRead);
        group.MapPost("/tables/jobs/{jobId}/cancel", CancelJob)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersRead);
        group.MapGet("/tables/jobs/{jobId}/download", DownloadJob)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersRead);
        return group;
    }

    private static async Task<IResult> BulkUsers(
        AdminBulkActionRequest request,
        UserManager<User> userManager,
        IdentityDbContext db,
        IAuditService audit,
        HttpContext http,
        CancellationToken ct)
    {
        if (request.RowKeys.Length is 0 or > 1000)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["rowKeys"] = ["Select between 1 and 1000 rows."] });
        if (request.ActionId is not ("activate" or "deactivate"))
            return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.UnsupportedBulkAction });

        var filter = IamTenantHttpContext.RequireFilter(http);

        if (request.Async)
            return await EnqueueBulkAsync("users", request, http, http.RequestServices.GetRequiredService<RedisAdminJobStore>(), filter, null, ct);

        var ids = request.RowKeys
            .Select(value => Guid.TryParse(value, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        var users = await db.Users.Where(user => ids.Contains(user.Id))
            .WhereTenantMembership(db, filter.AllowedTenantKeys)
            .ToListAsync(ct);
        foreach (var user in users)
            user.IsActive = request.ActionId == "activate";
        await db.SaveChangesAsync(ct);
        await AuditAsync(audit, http, "BULK_ACTION", "User", request.ActionId, ct);

        return Results.Ok(new { actionId = request.ActionId, requestedCount = request.RowKeys.Length, updatedCount = users.Count });
    }

    private static async Task<IResult> Export(
        string resource,
        AdminExportRequest request,
        IdentityDbContext db,
        IConglomerateTenantRegistry tenantRegistry,
        IAuditService audit,
        IAuthorizationService authorization,
        FacilityContext facilityContext,
        HttpContext http,
        CancellationToken ct)
    {
        if (!(await authorization.AuthorizeAsync(http.User, $"Permission:{HisHopePermissions.Reports.Export}")).Succeeded)
            return Results.Forbid();

        if (!request.MaskSensitive &&
            !(await authorization.AuthorizeAsync(http.User, $"Permission:{HisHopePermissions.Reports.Manage}")).Succeeded)
            return Results.Forbid();

        if (string.Equals(resource, "clients", StringComparison.OrdinalIgnoreCase) &&
            !(await authorization.AuthorizeAsync(http.User, AuthorizationPolicyNames.Permissions.AdminClientsRead)).Succeeded)
            return Results.Forbid();
        if (string.Equals(resource, "roles", StringComparison.OrdinalIgnoreCase) &&
            !(await authorization.AuthorizeAsync(http.User, AuthorizationPolicyNames.Permissions.AdminRolesRead)).Succeeded)
            return Results.Forbid();
        if (string.Equals(resource, "audit", StringComparison.OrdinalIgnoreCase) &&
            !(await authorization.AuthorizeAsync(http.User, AuthorizationPolicyNames.Permissions.AdminAuditRead)).Succeeded)
            return Results.Forbid();

        var format = request.Format?.Trim().ToLowerInvariant();
        if (format is not ("csv" or "json" or "xlsx"))
            return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.UnsupportedExportFormat });
        if (request.RowKeys.Length > 10000)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["rowKeys"] = ["Export is limited to 10000 rows."] });

        var filter = IamTenantHttpContext.RequireFilter(http);
        var allowedClientIds = IamTenantQueryExtensions.ResolveAllowedClientIds(tenantRegistry, filter);

        if (request.Async)
        {
            var store = http.RequestServices.GetRequiredService<RedisAdminJobStore>();
            var state = NewState("export", resource, string.Empty, request.RowKeys, request, http, filter, allowedClientIds, facilityContext);
            await store.CreateAndEnqueueAsync(state, ct);
            return Results.Accepted($"/api/v1/admin/tables/jobs/{state.JobId}", ToContract(state));
        }

        var rows = resource.ToLowerInvariant() switch
        {
            "users" => await ExportUsers(request, db, facilityContext, filter.AllowedTenantKeys, ct),
            "roles" => await ExportRoles(request, db, filter.AllowedTenantKeys, ct),
            "clients" => await ExportClients(request, db, allowedClientIds, ct),
            "audit" => await ExportAudit(request, db, filter.AllowedTenantKeys, ct),
            _ => null
        };
        if (rows is null)
            return Results.NotFound();
        ApplyExportPolicy(rows, request);

        await AuditAsync(audit, http, "EXPORT", resource, format!, ct);

        if (format == "json")
            return Results.File(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows)), "application/json", $"{resource}-export.json");

        if (format == "xlsx")
            return Results.File(ToXlsx(rows), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{resource}-export.xlsx");

        var csv = ToCsv(rows);
        return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", $"{resource}-export.csv");
    }

    private static async Task<IResult> BulkRoles(
        AdminBulkActionRequest request,
        IdentityDbContext db,
        IAuditService audit,
        HttpContext http,
        CancellationToken ct)
    {
        if (request.RowKeys.Length is 0 or > 1000)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["rowKeys"] = ["Select between 1 and 1000 rows."] });
        if (request.ActionId is not "delete")
            return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.UnsupportedBulkAction });

        var filter = IamTenantHttpContext.RequireFilter(http);

        if (request.Async)
            return await EnqueueBulkAsync("roles", request, http, http.RequestServices.GetRequiredService<RedisAdminJobStore>(), filter, null, ct);

        var ids = ParseIds(request.RowKeys);
        var roles = await FilterRolesByTenant(db.Roles.Where(role => ids.Contains(role.Id)), db, filter.AllowedTenantKeys).ToListAsync(ct);
        if (roles.Any(role => role.IsSystem))
            return Results.Conflict(new { errorCode = "system_role_protected" });
        var assigned = await db.UserRoles.AnyAsync(link => ids.Contains(link.RoleId), ct);
        if (assigned)
            return Results.Conflict(new { errorCode = "role_in_use" });

        db.Roles.RemoveRange(roles);
        await db.SaveChangesAsync(ct);
        await AuditAsync(audit, http, "BULK_ACTION", "Role", request.ActionId, ct);
        return Results.Ok(new { actionId = request.ActionId, requestedCount = request.RowKeys.Length, updatedCount = roles.Count });
    }

    private static async Task<IResult> BulkClients(
        AdminBulkActionRequest request,
        IdentityDbContext db,
        IConglomerateTenantRegistry tenantRegistry,
        IAuditService audit,
        HttpContext http,
        CancellationToken ct)
    {
        if (request.RowKeys.Length is 0 or > 1000)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["rowKeys"] = ["Select between 1 and 1000 rows."] });
        if (request.ActionId is not "delete")
            return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.UnsupportedBulkAction });

        var filter = IamTenantHttpContext.RequireFilter(http);
        var allowedClientIds = IamTenantQueryExtensions.ResolveAllowedClientIds(tenantRegistry, filter);

        if (request.Async)
            return await EnqueueBulkAsync("clients", request, http, http.RequestServices.GetRequiredService<RedisAdminJobStore>(), filter, allowedClientIds, ct);

        var ids = request.RowKeys.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToArray();
        var clientQuery = db.OpenIddictApplications.Where(client => client.Id != null && ids.Contains(client.Id));
        if (allowedClientIds is not null)
            clientQuery = clientQuery.Where(client => allowedClientIds.Contains(client.ClientId ?? string.Empty));
        var clients = await clientQuery.ToListAsync(ct);
        db.OpenIddictApplications.RemoveRange(clients);
        await db.SaveChangesAsync(ct);
        await AuditAsync(audit, http, "BULK_ACTION", "Client", request.ActionId, ct);
        return Results.Ok(new { actionId = request.ActionId, requestedCount = request.RowKeys.Length, updatedCount = clients.Count });
    }

    private static async Task<List<Dictionary<string, object?>>> ExportUsers(AdminExportRequest request, IdentityDbContext db, FacilityContext facilityContext, HashSet<string>? allowedTenantKeys, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking().WhereTenantMembership(db, allowedTenantKeys);
        if (!facilityContext.IsCrossFacility && facilityContext.AuthorizedFacilities.Count > 0)
        {
            var facilities = facilityContext.AuthorizedFacilities.ToArray();
            query = query.Where(user => user.FacilityMemberships.Any(membership =>
                membership.IsActive && membership.RevokedAt == null && facilities.Contains(membership.FacilityId)));
        }
        var ids = ParseIds(request.RowKeys);
        if (ids.Length > 0) query = query.Where(user => ids.Contains(user.Id));
        var users = await query.OrderBy(user => user.UserName).Take(10000).ToListAsync(ct);
        return users.Select(user => new Dictionary<string, object?>
        {
            ["id"] = user.Id,
            ["username"] = user.UserName,
            ["email"] = user.Email,
            ["active"] = user.IsActive,
            ["createdAt"] = user.CreatedAt
        }).ToList();
    }

    private static async Task<List<Dictionary<string, object?>>> ExportClients(AdminExportRequest request, IdentityDbContext db, HashSet<string>? allowedClientIds, CancellationToken ct)
    {
        var query = db.OpenIddictApplications.AsNoTracking();
        if (allowedClientIds is not null)
            query = query.Where(client => allowedClientIds.Contains(client.ClientId ?? string.Empty));
        var ids = request.RowKeys.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (ids.Length > 0) query = query.Where(client => client.Id != null && ids.Contains(client.Id));
        var clients = await query.OrderBy(client => client.ClientId).Take(10000).ToListAsync(ct);
        return clients.Select(client => new Dictionary<string, object?>
        {
            ["id"] = client.Id,
            ["clientId"] = client.ClientId,
            ["displayName"] = client.DisplayName,
            ["type"] = client.ClientType
        }).ToList();
    }

    private static async Task<List<Dictionary<string, object?>>> ExportRoles(AdminExportRequest request, IdentityDbContext db, HashSet<string>? allowedTenantKeys, CancellationToken ct)
    {
        var query = FilterRolesByTenant(db.Roles.AsNoTracking(), db, allowedTenantKeys);
        var ids = ParseIds(request.RowKeys);
        if (ids.Length > 0) query = query.Where(role => ids.Contains(role.Id));
        var roles = await query.OrderBy(role => role.Name).Take(10000).ToListAsync(ct);
        return roles.Select(role => new Dictionary<string, object?>
        {
            ["id"] = role.Id,
            ["name"] = role.Name,
            ["description"] = role.Description,
            ["system"] = role.IsSystem,
            ["createdAt"] = role.CreatedAt
        }).ToList();
    }

    private static async Task<List<Dictionary<string, object?>>> ExportAudit(AdminExportRequest request, IdentityDbContext db, HashSet<string>? allowedTenantKeys, CancellationToken ct)
    {
        var logs = await db.AuditLogs.AsNoTracking()
            .WhereTenantActor(db, allowedTenantKeys)
            .OrderByDescending(log => log.Timestamp)
            .Take(10000)
            .ToListAsync(ct);
        return logs.Select(log => new Dictionary<string, object?>
        {
            ["id"] = log.Id,
            ["userId"] = log.UserId,
            ["userName"] = log.UserName,
            ["action"] = log.Action,
            ["resourceType"] = log.ResourceType,
            ["resourceId"] = log.ResourceId,
            ["details"] = log.Details,
            ["ipAddress"] = log.IpAddress,
            ["timestamp"] = log.Timestamp
        }).ToList();
    }

    private static IQueryable<Role> FilterRolesByTenant(IQueryable<Role> query, IdentityDbContext db, HashSet<string>? allowedTenantKeys)
    {
        if (allowedTenantKeys is null)
            return query;

        var normalizedKeys = allowedTenantKeys.Select(key => key.ToLowerInvariant()).ToArray();
        return query.Where(role => db.Set<IdentityUserRole<Guid>>().Any(userRole =>
            userRole.RoleId == role.Id &&
            db.UserClaims.Any(claim =>
                claim.UserId == userRole.UserId &&
                claim.ClaimType == IamTenantScopeResolver.TenantMembershipClaimType &&
                normalizedKeys.Contains(claim.ClaimValue.ToLower()))));
    }

    private static Guid[] ParseIds(IEnumerable<string> values) => values
        .Select(value => Guid.TryParse(value, out var id) ? id : (Guid?)null)
        .Where(id => id.HasValue)
        .Select(id => id!.Value)
        .Distinct()
        .ToArray();

    private static string ToCsv(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        if (rows.Count == 0) return string.Empty;
        var columns = rows.SelectMany(row => row.Keys).Distinct().ToArray();
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', columns.Select(Escape)));
        foreach (var row in rows)
            builder.AppendLine(string.Join(',', columns.Select(column => Escape(row.GetValueOrDefault(column)))));
        return builder.ToString();
    }

    private static string Escape(object? value)
    {
        var text = value switch { null => string.Empty, DateTime date => date.ToString("O"), _ => Convert.ToString(value) ?? string.Empty };
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@') text = "'" + text;
        return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static void ApplyExportPolicy(List<Dictionary<string, object?>> rows, AdminExportRequest request)
    {
        var allowed = request.Columns?.Where(column => !string.IsNullOrWhiteSpace(column)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (allowed is { Count: > 0 })
                foreach (var key in row.Keys.Where(key => !allowed.Contains(key)).ToArray()) row.Remove(key);
            if (request.MaskSensitive)
                foreach (var key in row.Keys.Where(key => key.Equals("email", StringComparison.OrdinalIgnoreCase) || key.Equals("redirectUris", StringComparison.OrdinalIgnoreCase) || key.Contains("secret", StringComparison.OrdinalIgnoreCase) || key.Equals("jwks", StringComparison.OrdinalIgnoreCase)).ToArray()) row[key] = "[REDACTED]";
        }
    }

    private static byte[] ToXlsx(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var columns = rows.SelectMany(row => row.Keys).Distinct().ToArray();
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>
                """);
            WriteEntry(archive, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            WriteEntry(archive, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Export" sheetId="1" r:id="rId1"/></sheets></workbook>
                """);
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>
                """);

            var sheet = new XElement(XName.Get("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
                new XElement(XName.Get("sheetData", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
                    new XElement(XName.Get("row", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
                        columns.Select(column => Cell(column))),
                    rows.Select(row => new XElement(XName.Get("row", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
                        columns.Select(column => Cell(row.GetValueOrDefault(column)))))));
            WriteEntry(archive, "xl/worksheets/sheet1.xml", sheet.ToString(SaveOptions.DisableFormatting));
        }
        return output.ToArray();

        static XElement Cell(object? value) => new(
            XName.Get("c", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
            new XAttribute("t", "inlineStr"),
            new XElement(XName.Get("is", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
                new XElement(XName.Get("t", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
                    SpreadsheetSafe(Convert.ToString(value) ?? string.Empty))));
    }

    private static string SpreadsheetSafe(string value) => value.Length > 0 && value[0] is '=' or '+' or '-' or '@' ? "'" + value : value;

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static Task AuditAsync(IAuditService audit, HttpContext http, string action, string resource, string resourceId, CancellationToken ct) =>
        audit.LogPhiAccessAsync(new PhiAuditEntry
        {
            UserId = http.User.FindFirst("sub")?.Value ?? "system",
            UserRole = http.User.FindFirst("role")?.Value,
            ResourceType = resource,
            ResourceId = resourceId,
            Action = action,
            ClientIp = http.Connection.RemoteIpAddress?.ToString(),
            UserAgent = http.Request.Headers.UserAgent.ToString(),
            CorrelationId = http.Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? http.TraceIdentifier,
            HttpMethod = http.Request.Method,
            Path = http.Request.Path
        }, ct);

    private static async Task<IResult> EnqueueBulkAsync(
        string resource,
        AdminBulkActionRequest request,
        HttpContext http,
        RedisAdminJobStore store,
        IamTenantScopeFilter filter,
        HashSet<string>? allowedClientIds,
        CancellationToken ct)
    {
        var state = NewState("bulk", resource, request.ActionId, request.RowKeys, request, http, filter, allowedClientIds);
        foreach (var rowKey in request.RowKeys.Distinct(StringComparer.Ordinal))
            state.RowProgress[rowKey] = new BulkJobRowContract(rowKey, "queued");
        await store.CreateAndEnqueueAsync(state, ct);
        return Results.Accepted($"/api/v1/admin/tables/jobs/{state.JobId}", ToContract(state));
    }

    private static AdminJobState NewState(
        string kind,
        string resource,
        string actionId,
        string[] rowKeys,
        object request,
        HttpContext http,
        IamTenantScopeFilter filter,
        HashSet<string>? allowedClientIds,
        FacilityContext? facilityContext = null) => new()
        {
            JobId = Guid.NewGuid().ToString("N"),
            Kind = kind,
            Resource = resource.ToLowerInvariant(),
            ActionId = actionId,
            RowKeys = rowKeys,
            PayloadJson = JsonSerializer.Serialize(request),
            ActorSubject = http.User.FindFirst("sub")?.Value ?? "system",
            IsCrossFacility = facilityContext?.IsCrossFacility ?? false,
            AuthorizedFacilities = facilityContext?.AuthorizedFacilities.ToArray() ?? [],
            AllowedTenantKeys = filter.AllowedTenantKeys?.ToArray(),
            AllowedClientIds = allowedClientIds?.ToArray(),
            CorrelationId = http.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? http.TraceIdentifier,
            Total = rowKeys.Length
        };

    private static BulkJobContract ToContract(AdminJobState state) => new(
        state.JobId, state.Resource, state.ActionId, state.Status, state.Processed, state.Total,
        state.ErrorCode, state.CorrelationId, state.RowProgress.Values.ToArray(),
        state.ResultKey is null ? null : $"/api/v1/admin/tables/jobs/{state.JobId}/download");

    private static async Task<IResult> GetJob(string jobId, RedisAdminJobStore store, CancellationToken ct)
    {
        var state = await store.GetAsync(jobId, ct);
        return state is null ? Results.NotFound() : Results.Ok(ToContract(state));
    }

    private static async Task StreamJob(string jobId, RedisAdminJobStore store, HttpResponse response, CancellationToken ct)
    {
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";
        for (var attempt = 0; attempt < 600 && !ct.IsCancellationRequested; attempt++)
        {
            var state = await store.GetAsync(jobId, ct);
            if (state is null) { response.StatusCode = StatusCodes.Status404NotFound; return; }
            var payload = JsonSerializer.Serialize(ToContract(state));
            await response.WriteAsync($"event: job\ndata: {payload}\n\n", ct);
            await response.Body.FlushAsync(ct);
            if (state.Status is BulkJobStatus.Completed or BulkJobStatus.Failed or BulkJobStatus.Cancelled) return;
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
    }

    private static async Task<IResult> CancelJob(string jobId, RedisAdminJobStore store, CancellationToken ct)
    {
        var state = await store.GetAsync(jobId, ct);
        if (state is null) return Results.NotFound();
        await store.CancelAsync(state, ct);
        return Results.Ok(ToContract(state));
    }

    private static async Task<IResult> DownloadJob(string jobId, RedisAdminJobStore store, CancellationToken ct)
    {
        var result = await store.GetResultAsync(jobId, ct);
        return result is null
            ? Results.NotFound(new { errorCode = "job_result_unavailable" })
            : Results.File(result.Value.Content, result.Value.State.ContentType ?? "application/octet-stream", result.Value.State.FileName ?? $"{jobId}.bin");
    }

    internal static async Task ExecuteJobAsync(
        AdminJobState state,
        IdentityDbContext db,
        UserManager<User> userManager,
        IAuditService audit,
        RedisAdminJobStore store,
        CancellationToken ct)
    {
        var http = new DefaultHttpContext();
        http.TraceIdentifier = state.CorrelationId ?? state.JobId;
        http.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([
            new System.Security.Claims.Claim("sub", state.ActorSubject)
        ], "job"));
        http.Request.Method = "JOB";
        http.Request.Path = $"/api/v1/admin/tables/{state.Resource}/{state.Kind}";

        var allowedTenantKeys = state.AllowedTenantKeys is null
            ? null
            : state.AllowedTenantKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allowedClientIds = state.AllowedClientIds is null
            ? null
            : state.AllowedClientIds.ToHashSet(StringComparer.Ordinal);

        if (state.Kind == "export")
        {
            var request = JsonSerializer.Deserialize<AdminExportRequest>(state.PayloadJson) ?? throw new InvalidOperationException("Invalid export job payload.");
            var facilityContext = new FacilityContext
            {
                IsCrossFacility = state.IsCrossFacility,
                AuthorizedFacilities = state.AuthorizedFacilities.ToList()
            };
            var rows = state.Resource switch
            {
                "users" => await ExportUsers(request, db, facilityContext, allowedTenantKeys, ct),
                "roles" => await ExportRoles(request, db, allowedTenantKeys, ct),
                "clients" => await ExportClients(request, db, allowedClientIds, ct),
                "audit" => await ExportAudit(request, db, allowedTenantKeys, ct),
                _ => throw new InvalidOperationException("Unsupported export resource.")
            };
            ApplyExportPolicy(rows, request);
            var format = request.Format.Trim().ToLowerInvariant();
            var content = format == "json"
                ? Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows))
                : format == "xlsx" ? ToXlsx(rows) : Encoding.UTF8.GetBytes(ToCsv(rows));
            state.ContentType = format == "json" ? "application/json" : format == "xlsx" ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "text/csv; charset=utf-8";
            state.FileName = $"{state.Resource}-export.{format}";
            await store.SaveResultAsync(state, content, ct);
            if ((await store.GetAsync(state.JobId, ct))?.Status == BulkJobStatus.Cancelled)
                return;
            state.Status = BulkJobStatus.Completed;
            state.Processed = state.Total;
            await store.SaveAsync(state, ct);
            await AuditAsync(audit, http, "EXPORT", state.Resource, format, ct);
            return;
        }

        switch (state.Resource)
        {
            case "users":
                var userIds = ParseIds(state.RowKeys);
                foreach (var id in userIds)
                {
                    var user = await db.Users.Where(x => x.Id == id)
                        .WhereTenantMembership(db, allowedTenantKeys)
                        .SingleOrDefaultAsync(ct);
                    if (user is null)
                    {
                        await CompleteRowAsync(state, store, id.ToString(), "not_found", ct);
                        continue;
                    }
                    user.IsActive = state.ActionId == "activate";
                    await db.SaveChangesAsync(ct);
                    await CompleteRowAsync(state, store, id.ToString(), "completed", ct);
                }
                break;
            case "roles":
                foreach (var id in ParseIds(state.RowKeys))
                {
                    var role = await FilterRolesByTenant(db.Roles.Where(x => x.Id == id), db, allowedTenantKeys).SingleOrDefaultAsync(ct);
                    if (role is null) { await CompleteRowAsync(state, store, id.ToString(), "not_found", ct); continue; }
                    if (role.IsSystem) throw new InvalidOperationException("system_role_protected");
                    if (await db.UserRoles.AnyAsync(x => x.RoleId == id, ct)) throw new InvalidOperationException("role_in_use");
                    db.Roles.Remove(role);
                    await db.SaveChangesAsync(ct);
                    await CompleteRowAsync(state, store, id.ToString(), "completed", ct);
                }
                break;
            case "clients":
                foreach (var id in state.RowKeys.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
                {
                    var clientQuery = db.OpenIddictApplications.Where(x => x.Id == id);
                    if (allowedClientIds is not null)
                        clientQuery = clientQuery.Where(x => allowedClientIds.Contains(x.ClientId ?? string.Empty));
                    var client = await clientQuery.SingleOrDefaultAsync(ct);
                    if (client is not null) { db.OpenIddictApplications.Remove(client); await db.SaveChangesAsync(ct); }
                    await CompleteRowAsync(state, store, id, client is null ? "not_found" : "completed", ct);
                }
                break;
            default: throw new InvalidOperationException("Unsupported bulk resource.");
        }
        if (state.Status == BulkJobStatus.Cancelled)
            return;
        state.Status = BulkJobStatus.Completed;
        await store.SaveAsync(state, ct);
        await AuditAsync(audit, http, "BULK_ACTION", state.Resource, state.ActionId, ct);
    }

    private static async Task CompleteRowAsync(AdminJobState state, RedisAdminJobStore store, string rowKey, string status, CancellationToken ct)
    {
        if ((await store.GetAsync(state.JobId, ct))?.Status == BulkJobStatus.Cancelled)
        {
            state.Status = BulkJobStatus.Cancelled;
            return;
        }
        state.RowProgress[rowKey] = new BulkJobRowContract(rowKey, status);
        state.Processed = state.RowProgress.Values.Count(x => x.Status is "completed" or "not_found");
        await store.SaveAsync(state, ct);
    }

    internal sealed record AdminBulkActionRequest(string ActionId, string[] RowKeys, JsonElement Query, JsonElement? Selection, bool Async = false);
    internal sealed record AdminExportRequest(string Format, string[] Columns, string[] RowKeys, JsonElement Query, bool Async = false, bool MaskSensitive = true);
}
