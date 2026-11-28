using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using His.Hope.Authorization;
using His.Hope.ContentService.Api;
using His.Hope.ContentService.Application;
using His.Hope.ContentService.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace ContentService.Integration.Tests;

public sealed class ContentPublicApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ContentPublicApiTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                "ConnectionStrings:ContentDb",
                Environment.GetEnvironmentVariable("DATABASE_CONTENT_URL")
                    ?? "Host=localhost;Port=5433;Database=contentdb;Username=postgres;Password=postgres");
        });

    [Fact]
    public async Task Public_home_returns_seed_banners_and_articles()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/content/public/home?tenantKey=customer-factory-x");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var home = await response.Content.ReadFromJsonAsync<HomeContentDto>();
        Assert.NotNull(home);
        Assert.True(home!.Banners.Count >= 3);
        Assert.True(home.Articles.Count >= 1);
    }

    [Fact]
    public async Task Public_home_accepts_canonical_tenant_header()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/content/public/home");
        request.Headers.Add("X-HisHope-Tenant", "customer-factory-x");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var home = await response.Content.ReadFromJsonAsync<HomeContentDto>();
        Assert.NotNull(home);
        Assert.True(home!.Articles.Count >= 1);
    }

    [Fact]
    public async Task Public_home_does_not_expose_an_unallowlisted_tenant()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/content/public/home");
        request.Headers.Add("X-HisHope-Tenant", "customer-acme");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var home = await response.Content.ReadFromJsonAsync<HomeContentDto>();
        Assert.NotNull(home);
        Assert.All(home!.Banners, banner => Assert.Equal("customer-factory-x", banner.TenantKey));
    }

    [Fact]
    public async Task Public_article_by_slug_returns_published_article()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/content/public/articles/gioi-thieu-chom-chom-say-deo?tenantKey=customer-factory-x");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var article = await response.Content.ReadFromJsonAsync<ArticleDto>();
        Assert.Equal("Giới thiệu chôm chôm sấy dẻo — món lạ từ Nacoms", article!.Title);
    }

    [Fact]
    public async Task Partnership_inquiry_can_be_submitted_anonymously()
    {
        var client = _factory.CreateClient();
        var payload = new CreatePartnershipInquiryRequest(
            "Acme Dist",
            "Jane Doe",
            "jane@acme.example",
            "+84901234567",
            "distributor",
            "Interested in wholesale.");

        var response = await client.PostAsJsonAsync(
            "/api/v1/content/public/partnership-inquiries?tenantKey=customer-factory-x",
            payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Sitemap_xml_is_available()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/content/sitemap.xml?tenantKey=customer-factory-x&baseUrl=http://localhost:4205");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("blog/gioi-thieu-chom-chom-say-deo", body);
    }
}

public sealed class ContentManageApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ContentManageApiTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                "ConnectionStrings:ContentDb",
                Environment.GetEnvironmentVariable("DATABASE_CONTENT_URL")
                    ?? "Host=localhost;Port=5433;Database=contentdb;Username=postgres;Password=postgres");
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                services.PostConfigureAll<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                });
            });
        });

    [Fact]
    public async Task Manage_articles_requires_content_manage_permission()
    {
        var client = CreateClient(["content.read"]);
        var response = await client.GetAsync("/api/v1/content/articles?tenantKey=customer-factory-x");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manage_articles_accepts_content_manage_permission()
    {
        var client = CreateClient(["content.manage"]);
        var response = await client.GetAsync("/api/v1/content/articles?tenantKey=customer-factory-x");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Media_upload_rejects_non_image_content_types()
    {
        var client = CreateClient(["content.manage"]);
        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent("<script>alert(1)</script>"u8.ToArray());
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
        form.Add(content, "file", "payload.html");

        var response = await client.PostAsync("/api/v1/content/media/upload", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unsupported_file_type", problem.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Manage_articles_enforces_publication_state_transitions()
    {
        var client = CreateClient(["content.manage"]);
        var slug = $"transition-{Guid.NewGuid():N}";
        var draft = new UpsertArticleRequest(
            slug, "Transition", "Excerpt", "<p>Body</p>", "News", "https://example.test/image.jpg",
            "en-US", ContentArticleStatuses.Draft, null, null, null, null);

        var created = await client.PostAsJsonAsync("/api/v1/content/articles", draft);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var article = await created.Content.ReadFromJsonAsync<ArticleDto>();
        Assert.NotNull(article);

        var review = draft with { Status = ContentArticleStatuses.InReview };
        var reviewResponse = await client.PutAsJsonAsync($"/api/v1/content/articles/{article!.Id}", review);
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);

        var published = review with { Status = ContentArticleStatuses.Published };
        var publishResponse = await client.PutAsJsonAsync($"/api/v1/content/articles/{article.Id}", published);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        var publishedArticle = await publishResponse.Content.ReadFromJsonAsync<ArticleDto>();
        Assert.NotNull(publishedArticle);
        Assert.True(publishedArticle!.PublishedAt > article.PublishedAt);

        var revertResponse = await client.PutAsJsonAsync(
            $"/api/v1/content/articles/{article.Id}", draft);
        Assert.Equal(HttpStatusCode.Conflict, revertResponse.StatusCode);
        var problem = await revertResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_status_transition", problem.GetProperty("errorCode").GetString());
    }

    private HttpClient CreateClient(IEnumerable<string> permissions)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", string.Join(',', permissions));
        client.DefaultRequestHeaders.Add("X-Test-Tenant", "customer-factory-x");
        return client;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var permissions = Request.Headers["X-Test-Permissions"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
            var tenant = Request.Headers["X-Test-Tenant"].ToString();
            var claims = new List<Claim>
            {
                new("sub", "operator-test"),
                new("tenant_id", tenant),
                new("portal_class", PortalClassConstants.Operator),
            };
            claims.AddRange(permissions.Select(permission => new Claim("permissions", permission)));
            var identity = new ClaimsIdentity(claims, "Test");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
        }
    }
}
