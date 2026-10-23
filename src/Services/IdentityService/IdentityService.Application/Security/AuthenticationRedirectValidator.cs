using Microsoft.Extensions.Configuration;

namespace His.Hope.IdentityService.Application.Security;

public static class AuthenticationRedirectValidator
{
    public static string ResolveSafeReturnUrl(
        string? returnUrl,
        IConfiguration configuration,
        string? referer = null,
        string? spaOriginHint = null)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return "/";

        if (IsSpaAuthRelativePath(returnUrl))
            return ResolveSpaAuthReturnUrl(returnUrl, configuration, referer, spaOriginHint);

        if (returnUrl.StartsWith("/", StringComparison.Ordinal) &&
            !returnUrl.StartsWith("//", StringComparison.Ordinal) &&
            !returnUrl.Contains('\\') &&
            !returnUrl.Contains(':'))
            return returnUrl;

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var absolute))
            return "/";

        return IsWhitelistedAbsoluteUrl(absolute, returnUrl, configuration)
            ? returnUrl
            : "/";
    }

    private static bool IsSpaAuthRelativePath(string returnUrl) =>
        returnUrl.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase);

    private static string ResolveSpaAuthReturnUrl(
        string spaPath,
        IConfiguration configuration,
        string? referer,
        string? spaOriginHint)
    {
        foreach (var origin in CandidateSpaOrigins(referer, spaOriginHint, configuration))
        {
            if (!IsWhitelistedOrigin(origin, configuration))
                continue;

            var candidate = $"{origin.TrimEnd('/')}{spaPath}";
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var absolute) &&
                IsWhitelistedAbsoluteUrl(absolute, candidate, configuration))
                return candidate;
        }

        return "/";
    }

    /// <summary>
    /// Reconstructs an Identity /Account/Login redirect when a browser lands on
    /// /auth/login on the Identity host (legacy or misrouted relative URLs).
    /// </summary>
    public static string TryBuildAccountLoginRedirect(
        string? innerReturnUrl,
        IConfiguration configuration,
        string? referer = null,
        string? spaOriginHint = null)
    {
        var safeInnerReturn = string.IsNullOrWhiteSpace(innerReturnUrl) ? "/dashboard" : innerReturnUrl;
        if (!safeInnerReturn.StartsWith("/", StringComparison.Ordinal))
            safeInnerReturn = $"/{safeInnerReturn}";

        var spaCallbackPath = $"/auth/login?returnUrl={Uri.EscapeDataString(safeInnerReturn)}";
        var absoluteCallback = ResolveSpaAuthReturnUrl(
            spaCallbackPath,
            configuration,
            referer,
            spaOriginHint);
        if (absoluteCallback == "/")
            return "/Account/Login";

        var originHint = spaOriginHint;
        if (string.IsNullOrWhiteSpace(originHint))
        {
            foreach (var origin in CandidateSpaOrigins(referer, spaOriginHint, configuration))
            {
                originHint = origin;
                break;
            }
        }

        var accountLogin = $"/Account/Login?returnUrl={Uri.EscapeDataString(absoluteCallback)}";
        return string.IsNullOrWhiteSpace(originHint)
            ? accountLogin
            : $"{accountLogin}&spaOrigin={Uri.EscapeDataString(originHint.Trim())}";
    }

    private static IEnumerable<string> CandidateSpaOrigins(
        string? referer,
        string? spaOriginHint,
        IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(spaOriginHint) &&
            Uri.TryCreate(spaOriginHint.Trim(), UriKind.Absolute, out var hintedOrigin))
            yield return hintedOrigin.GetLeftPart(UriPartial.Authority);

        if (!string.IsNullOrWhiteSpace(referer) &&
            Uri.TryCreate(referer.Trim(), UriKind.Absolute, out var refererUri))
            yield return refererUri.GetLeftPart(UriPartial.Authority);
    }

    private static bool IsWhitelistedOrigin(string origin, IConfiguration configuration)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var absolute))
            return false;

        return IsWhitelistedAbsoluteUrl(absolute, origin, configuration);
    }

    private static bool IsWhitelistedAbsoluteUrl(Uri absolute, string returnUrl, IConfiguration configuration)
    {
        var whitelist = configuration.GetSection("Authentication:RedirectWhitelist").Get<string[]>() ?? [];
        foreach (var allowed in whitelist)
        {
            if (string.IsNullOrWhiteSpace(allowed))
                continue;

            if (allowed.StartsWith("/", StringComparison.Ordinal))
            {
                if (returnUrl.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
                    return true;
                continue;
            }

            if (!Uri.TryCreate(allowed, UriKind.Absolute, out var allowedUri))
                continue;

            if (string.Equals(absolute.Scheme, allowedUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(absolute.Host, allowedUri.Host, StringComparison.OrdinalIgnoreCase) &&
                absolute.Port == allowedUri.Port &&
                absolute.AbsoluteUri.StartsWith(
                    allowedUri.GetLeftPart(UriPartial.Authority) + "/",
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
