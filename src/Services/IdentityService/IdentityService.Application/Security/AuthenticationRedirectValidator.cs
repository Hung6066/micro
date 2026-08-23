using Microsoft.Extensions.Configuration;

namespace His.Hope.IdentityService.Application.Security;

public static class AuthenticationRedirectValidator
{
    public static string ResolveSafeReturnUrl(string? returnUrl, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return "/";

        if (returnUrl.StartsWith("/", StringComparison.Ordinal) &&
            !returnUrl.StartsWith("//", StringComparison.Ordinal) &&
            !returnUrl.Contains('\\') &&
            !returnUrl.Contains(':'))
            return returnUrl;

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var absolute))
            return "/";

        var whitelist = configuration.GetSection("Authentication:RedirectWhitelist").Get<string[]>() ?? [];
        foreach (var allowed in whitelist)
        {
            if (string.IsNullOrWhiteSpace(allowed))
                continue;

            if (allowed.StartsWith("/", StringComparison.Ordinal))
            {
                if (returnUrl.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
                    return returnUrl;
                continue;
            }

            if (!Uri.TryCreate(allowed, UriKind.Absolute, out var allowedUri))
                continue;

            if (string.Equals(absolute.Scheme, allowedUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(absolute.Host, allowedUri.Host, StringComparison.OrdinalIgnoreCase) &&
                absolute.AbsoluteUri.StartsWith(allowedUri.AbsoluteUri.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))
                return returnUrl;
        }

        return "/";
    }
}
