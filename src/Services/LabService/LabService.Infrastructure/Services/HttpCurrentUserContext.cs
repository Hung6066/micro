using System.Security.Claims;
using His.Hope.LabService.Application.Common.Abstractions;
using Microsoft.AspNetCore.Http;

namespace His.Hope.LabService.Infrastructure.Services;

internal sealed class HttpCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string UserId => IsAuthenticated ? GetUser().FindFirst("sub")?.Value ?? string.Empty : "system";

    public string FullName => IsAuthenticated ? GetUser().FindFirst("fullName")?.Value ?? string.Empty : "System";

    public bool IsAuthenticated => GetUser().Identity?.IsAuthenticated == true;

    private ClaimsPrincipal GetUser() => _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
}
