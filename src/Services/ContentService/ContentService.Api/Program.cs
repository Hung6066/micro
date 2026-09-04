using System.Security.Cryptography.X509Certificates;
using His.Hope.ContentService.Domain;
using System.Text;
using His.Hope.Authorization;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.ContentService.Api;
using His.Hope.ContentService.Application;
using His.Hope.ContentService.Infrastructure;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Middleware;
using His.Hope.Configuration;
using His.Hope.ServiceDefaults;
using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHisHopeTenantPlacement(builder.Configuration);
builder.Services.AddContentInfrastructure(builder.Configuration);
builder.Services.AddHisHopeServicePlatform(builder.Configuration, "content-service");
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ContentDbContext>("content-db");
// Keep the Content key ring stable across container replacement. The
// directory is backed by a dedicated deployment volume (or an encrypted host
// mount in production).
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("His.Hope.ContentService")
    .PersistKeysToFileSystem(new DirectoryInfo(
        builder.Configuration["DataProtection:KeysPath"]
            ?? "/var/lib/his-hope/content-data-protection-keys"));
var dataProtectionCertificatePath = builder.Configuration["DataProtection:CertificatePath"];
if (!string.IsNullOrWhiteSpace(dataProtectionCertificatePath))
{
    if (!File.Exists(dataProtectionCertificatePath))
    {
        throw new InvalidOperationException(
            $"DataProtection certificate was configured but not found: {dataProtectionCertificatePath}");
    }

    dataProtection.ProtectKeysWithCertificate(new X509Certificate2(
        dataProtectionCertificatePath,
        builder.Configuration["DataProtection:CertificatePassword"]));
}
else if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "ContentService requires DataProtection:CertificatePath outside Development.");
}
builder.Services.AddHisHopeDpopValidation();
His.Hope.AspNetCore.Authentication.JwtAuthenticationExtensions.AddHisHopeJwtAuthentication(builder.Services, builder.Configuration);
builder.Services.AddHisHopeAuthorization();
builder.Services.AddAuthorizationBuilder()
    .AddContentAuthorizationPolicies();

var app = builder.Build();
app.UseGlobalExceptionHandler();
app.UseHisHopeServiceDefaults();
app.UseDpopAuthorizationSchemeNormalization();
app.UseAuthentication();
app.UseDpopAccessTokenValidation();
app.UseAuthorization();
app.UseHisHopeTenantScope();

app.ValidateHisHopeTenantPlacement();

var runContentMigrations = builder.Configuration.GetValue("Persistence:RunMigrationsOnStartup", false) ||
    builder.Configuration.GetValue("Persistence:MigrationOnly", false);
if (runContentMigrations)
{
    app.Services.MigrateContentDatabase();
}
else if (!app.Environment.IsDevelopment() &&
         !string.Equals(app.Environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "ContentService requires Persistence:RunMigrationsOnStartup or Persistence:MigrationOnly outside Development.");
}

if (builder.Configuration.GetValue("Persistence:MigrationOnly", false))
{
    return;
}

var uploadRoot = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadRoot);
var allowedUploadTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["image/jpeg"] = ".jpg",
    ["image/png"] = ".png",
    ["image/webp"] = ".webp",
    ["image/avif"] = ".avif",
};
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadRoot),
    RequestPath = "/uploads",
});

static IResult ContentProblem(int statusCode, string errorCode) =>
    Results.Problem(
        statusCode: statusCode,
        extensions: new Dictionary<string, object?> { ["errorCode"] = errorCode });

var contentPublic = app.MapGroup("/api/v1/content/public").AllowAnonymous();

contentPublic.MapGet("/home", (HttpContext context, PostgresContentStore store) =>
{
    var tenantKey = ContentHttpExtensions.ResolvePublicTenant(context);
    return Results.Ok(store.GetHome(tenantKey));
});

contentPublic.MapGet("/banners", (HttpContext context, PostgresContentStore store) =>
{
    var tenantKey = ContentHttpExtensions.ResolvePublicTenant(context);
    return Results.Ok(new { items = store.ListBanners(tenantKey, publishedOnly: true) });
});

contentPublic.MapGet("/stories", (HttpContext context, PostgresContentStore store) =>
{
    var tenantKey = ContentHttpExtensions.ResolvePublicTenant(context);
    return Results.Ok(new { items = store.ListStories(tenantKey, publishedOnly: true) });
});

contentPublic.MapGet("/articles", (HttpContext context, PostgresContentStore store, [FromQuery] string? locale) =>
{
    var tenantKey = ContentHttpExtensions.ResolvePublicTenant(context);
    return Results.Ok(new { items = store.ListArticles(tenantKey, publishedOnly: true, locale) });
});

contentPublic.MapGet("/articles/{slug}", (string slug, HttpContext context, PostgresContentStore store) =>
{
    var tenantKey = ContentHttpExtensions.ResolvePublicTenant(context);
    var article = store.GetArticleBySlug(tenantKey, slug);
    if (article is null || !string.Equals(article.Status, ContentArticleStatuses.Published, StringComparison.OrdinalIgnoreCase))
        return ContentProblem(StatusCodes.Status404NotFound, "not_found");
    return Results.Ok(article);
});

contentPublic.MapPost("/partnership-inquiries", (
    HttpContext context,
    PostgresContentStore store,
    [FromBody] CreatePartnershipInquiryRequest request) =>
{
    var validationError = ContentPolicies.ValidatePartnershipInquiry(request);
    if (validationError is not null)
        return ContentProblem(StatusCodes.Status400BadRequest, validationError);

    var tenantKey = ContentHttpExtensions.ResolvePublicTenant(context);
    var inquiry = store.CreateInquiry(tenantKey, request);
    return Results.Created($"/api/v1/content/inquiries/{inquiry.Id}", inquiry);
});

contentPublic.MapPost("/newsletter/subscriptions", (
    HttpContext context,
    PostgresContentStore store,
    [FromBody] SubscribeNewsletterRequest request) =>
{
    if (ContentPolicies.ValidateNewsletterEmail(request.Email) is not null)
        return ContentProblem(StatusCodes.Status400BadRequest, "validation_failed");

    var tenantKey = ContentHttpExtensions.ResolvePublicTenant(context);
    return Results.Ok(store.SubscribeNewsletter(tenantKey, request.Email));
});

app.MapGet("/api/v1/content/sitemap.xml", (HttpContext context, PostgresContentStore store) =>
{
    var tenantKey = ContentHttpExtensions.ResolvePublicTenant(context);
    var baseUrl = context.Request.Query["baseUrl"].FirstOrDefault() ?? "http://localhost:4205";
    var xml = store.BuildSitemapXml(tenantKey, baseUrl);
    return Results.Content(xml, "application/xml", Encoding.UTF8);
}).AllowAnonymous();

app.MapGet("/api/v1/content/rss.xml", (HttpContext context, PostgresContentStore store) =>
{
    var tenantKey = ContentHttpExtensions.ResolvePublicTenant(context);
    var baseUrl = context.Request.Query["baseUrl"].FirstOrDefault() ?? "http://localhost:4205";
    var title = context.Request.Query["title"].FirstOrDefault() ?? "Nacoms Blog";
    var xml = store.BuildRssXml(tenantKey, baseUrl, title);
    return Results.Content(xml, "application/rss+xml", Encoding.UTF8);
}).AllowAnonymous();

var content = app.MapGroup("/api/v1/content")
    .RequireAuthorization()
    .RequireTenantContext();

content.MapGet("/banners", (HttpContext context, PostgresContentStore store) =>
{
    var tenantKey = ContentHttpExtensions.ResolveManageTenant(context);
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();
    return Results.Ok(new { items = store.ListBanners(tenantKey, publishedOnly: false) });
})
.RequireAuthorization(
    ContentAuthorizationPolicies.Manage,
    AuthorizationPolicyNames.Permissions.ContentManage);

content.MapPost("/banners", (
    HttpContext context,
    PostgresContentStore store,
    [FromBody] UpsertBannerRequest request) =>
{
    var tenantKey = ContentHttpExtensions.ResolveManageTenant(context, isMutation: true);
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();
    var banner = store.UpsertBanner(null, tenantKey, request);
    return Results.Created($"/api/v1/content/banners/{banner.Id}", banner);
})
.RequireAuthorization(
    ContentAuthorizationPolicies.Manage,
    AuthorizationPolicyNames.Permissions.ContentManage);

content.MapPut("/banners/{bannerId:guid}", (
    Guid bannerId,
    HttpContext context,
    PostgresContentStore store,
    [FromBody] UpsertBannerRequest request) =>
{
    var tenantKey = ContentHttpExtensions.ResolveManageTenant(context, isMutation: true);
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();
    if (store.GetBanner(bannerId, tenantKey) is null)
        return ContentProblem(StatusCodes.Status404NotFound, "not_found");
    return Results.Ok(store.UpsertBanner(bannerId, tenantKey, request));
})
.RequireAuthorization(
    ContentAuthorizationPolicies.Manage,
    AuthorizationPolicyNames.Permissions.ContentManage);

content.MapDelete("/banners/{bannerId:guid}", (
    Guid bannerId,
    HttpContext context,
    PostgresContentStore store) =>
{
    var tenantKey = ContentHttpExtensions.ResolveManageTenant(context, isMutation: true);
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();
    return store.DeleteBanner(bannerId, tenantKey)
        ? Results.NoContent()
        : ContentProblem(StatusCodes.Status404NotFound, "not_found");
})
.RequireAuthorization(
    ContentAuthorizationPolicies.Manage,
    AuthorizationPolicyNames.Permissions.ContentManage);

content.MapGet("/stories", (HttpContext context, PostgresContentStore store) =>
{
    var tenantKey = ContentHttpExtensions.ResolveManageTenant(context);
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();
    return Results.Ok(new { items = store.ListStories(tenantKey, publishedOnly: false) });
})
.RequireAuthorization(
    ContentAuthorizationPolicies.Manage,
    AuthorizationPolicyNames.Permissions.ContentManage);

content.MapPost("/stories", (
    HttpContext context,
    PostgresContentStore store,
    [FromBody] UpsertStoryBlockRequest request) =>
{
    var tenantKey = ContentHttpExtensions.ResolveManageTenant(context, isMutation: true);
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();
    var story = store.UpsertStory(null, tenantKey, request);
    return Results.Created($"/api/v1/content/stories/{story.Id}", story);
})
.RequireAuthorization(
    ContentAuthorizationPolicies.Manage,
    AuthorizationPolicyNames.Permissions.ContentManage);

content.MapPut("/stories/{storyId:guid}", (
    Guid storyId,
    HttpContext context,
    PostgresContentStore store,
    [FromBody] UpsertStoryBlockRequest request) =>
{
    var tenantKey = ContentHttpExtensions.ResolveManageTenant(context, isMutation: true);
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();
    if (store.GetStory(storyId, tenantKey) is null)
        return ContentProblem(StatusCodes.Status404NotFound, "not_found");
    return Results.Ok(store.UpsertStory(storyId, tenantKey, request));
})
.RequireAuthorization(
    ContentAuthorizationPolicies.Manage,
    AuthorizationPolicyNames.Permissions.ContentManage);

content.MapDelete("/stories/{storyId:guid}", (
    Guid storyId,
    HttpContext context,
    PostgresContentStore store) =>
{
    var tenantKey = ContentHttpExtensions.ResolveManageTenant(context, isMutation: true);
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();
    return store.DeleteStory(storyId, tenantKey)
        ? Results.NoContent()
        : ContentProblem(StatusCodes.Status404NotFound, "not_found");
})
.RequireAuthorization(
    ContentAuthorizationPolicies.Manage,
    AuthorizationPolicyNames.Permissions.ContentManage);

content.MapGet("/articles", (HttpContext context, PostgresContentStore store, [FromQuery] string? locale) =>
{
    var tenantKey = ContentHttpExtensions.ResolveManageTenant(context);
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();
    return Results.Ok(new { items = store.ListArticles(tenantKey, publishedOnly: false, locale) });
})
.RequireAuthorization(
    ContentAuthorizationPolicies.Manage,
    AuthorizationPolicyNames.Permissions.ContentManage);

content.MapPost("/articles", (
    HttpContext context,
    PostgresContentStore store,
    [FromBody] UpsertArticleRequest request) =>
{
    var tenantKey = ContentHttpExtensions.ResolveManageTenant(context, isMutation: true);
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();
    if (ContentPolicies.ValidateArticleStatus(request.Status) is not null)
        return ContentProblem(StatusCodes.Status400BadRequest, "invalid_status");
    var article = store.UpsertArticle(null, tenantKey, request);
    return Results.Created($"/api/v1/content/articles/{article.Id}", article);
})
.RequireAuthorization(
    ContentAuthorizationPolicies.Manage,
    AuthorizationPolicyNames.Permissions.ContentManage);

content.MapPut("/articles/{articleId:guid}", (
    Guid articleId,
    HttpContext context,
    PostgresContentStore store,
    [FromBody] UpsertArticleRequest request) =>
{
    var tenantKey = ContentHttpExtensions.ResolveManageTenant(context, isMutation: true);
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();
    var existingArticle = store.GetArticle(articleId, tenantKey);
    if (existingArticle is null)
        return ContentProblem(StatusCodes.Status404NotFound, "not_found");
    if (ContentPolicies.ValidateArticleStatus(request.Status) is not null)
        return ContentProblem(StatusCodes.Status400BadRequest, "invalid_status");
    if (!ContentPolicies.CanTransitionArticleStatus(existingArticle.Status, request.Status))
        return ContentProblem(StatusCodes.Status409Conflict, "invalid_status_transition");
    return Results.Ok(store.UpsertArticle(articleId, tenantKey, request));
})
.RequireAuthorization(
    ContentAuthorizationPolicies.Manage,
    AuthorizationPolicyNames.Permissions.ContentManage);

content.MapDelete("/articles/{articleId:guid}", (
    Guid articleId,
    HttpContext context,
    PostgresContentStore store) =>
{
    var tenantKey = ContentHttpExtensions.ResolveManageTenant(context, isMutation: true);
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();
    return store.DeleteArticle(articleId, tenantKey)
        ? Results.NoContent()
        : ContentProblem(StatusCodes.Status404NotFound, "not_found");
})
.RequireAuthorization(
    ContentAuthorizationPolicies.Manage,
    AuthorizationPolicyNames.Permissions.ContentManage);

content.MapGet("/inquiries", (HttpContext context, PostgresContentStore store) =>
{
    var tenantKey = ContentHttpExtensions.ResolveManageTenant(context);
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();
    return Results.Ok(new { items = store.ListInquiries(tenantKey) });
})
.RequireAuthorization(
    ContentAuthorizationPolicies.InquiriesView,
    AuthorizationPolicyNames.Permissions.ContentInquiriesView);

content.MapPatch("/inquiries/{inquiryId:guid}/status", (
    Guid inquiryId,
    HttpContext context,
    PostgresContentStore store,
    [FromBody] UpdateInquiryStatusRequest request) =>
{
    var tenantKey = ContentHttpExtensions.ResolveManageTenant(context, isMutation: true);
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();
    if (ContentPolicies.ValidateInquiryStatus(request.Status) is not null)
        return ContentProblem(StatusCodes.Status400BadRequest, "invalid_status");
    var inquiry = store.UpdateInquiryStatus(inquiryId, tenantKey, request.Status);
    return inquiry is null
        ? ContentProblem(StatusCodes.Status404NotFound, "not_found")
        : Results.Ok(inquiry);
})
.RequireAuthorization(
    ContentAuthorizationPolicies.InquiriesView,
    AuthorizationPolicyNames.Permissions.ContentInquiriesView);

content.MapGet("/media", (HttpContext context, PostgresContentStore store) =>
{
    var tenantKey = ContentHttpExtensions.ResolveManageTenant(context);
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();
    return Results.Ok(new { items = store.ListMedia(tenantKey) });
})
.RequireAuthorization(
    ContentAuthorizationPolicies.Manage,
    AuthorizationPolicyNames.Permissions.ContentManage);

content.MapPost("/media/upload", async (
    HttpContext context,
    PostgresContentStore store,
    IFormFile file) =>
{
    var tenantKey = ContentHttpExtensions.ResolveManageTenant(context, isMutation: true);
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();

    if (file.Length <= 0 || file.Length > 5 * 1024 * 1024)
        return ContentProblem(StatusCodes.Status400BadRequest, "invalid_file");

    if (!allowedUploadTypes.TryGetValue(file.ContentType, out var extension))
        return ContentProblem(StatusCodes.Status400BadRequest, "unsupported_file_type");

    var safeName = $"{Guid.NewGuid():N}{extension}";
    var path = Path.Combine(uploadRoot, safeName);
    await using (var stream = File.Create(path))
        await file.CopyToAsync(stream);

    var publicUrl = $"{context.Request.Scheme}://{context.Request.Host}/uploads/{safeName}";
    var asset = store.RegisterMedia(tenantKey, file.FileName, file.ContentType, publicUrl, file.Length);
    return Results.Created(publicUrl, asset);
})
.DisableAntiforgery()
.RequireAuthorization(
    ContentAuthorizationPolicies.Manage,
    AuthorizationPolicyNames.Permissions.ContentManage);

app.MapHisHopeHealthEndpoints();
app.Run();
