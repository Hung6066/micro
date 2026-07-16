using Microsoft.AspNetCore.Http;

namespace His.Hope.Infrastructure.Security;

/// <summary>
/// Security headers middleware implementing HIPAA-recommended HTTP security headers.
/// SECURITY: This middleware adds defense-in-depth headers to protect against
/// XSS, clickjacking, MIME sniffing, and protocol downgrade attacks.
/// 
/// References:
///   - OWASP Secure Headers Project
///   - HIPAA Security Rule (164.312(e)(1)) - Transmission Security
///   - NIST SP 800-53 SC-8 - Transmission Confidentiality and Integrity
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) =>
        _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // === Anti-MIME-Sniffing ===
        // Prevents browser from MIME-sniffing responses away from declared Content-Type
        headers["X-Content-Type-Options"] = "nosniff";

        // === Clickjacking Protection ===
        // Prevents the page from being rendered in a frame/iframe
        headers["X-Frame-Options"] = "DENY";

        // === Referrer Policy ===
        // Only send the origin as the Referer header when navigating cross-origin
        // SECURITY: Prevents leaking full URLs in Referer header (HIPAA PHI protection)
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // === Permissions Policy ===
        // Restricts browser API access (camera, microphone, geolocation)
        // SECURITY: Healthcare applications should not need these APIs
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        // === Cross-Origin Isolation ===
        // Prevents cross-origin reads of embedded resources
        headers["Cross-Origin-Embedder-Policy"] = "require-corp";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";

        // === HTTP Strict Transport Security (HSTS) ===
        // FIXED: Previously only added HSTS on HTTP (inverted logic - security bug).
        // Now correctly adds HSTS on HTTPS connections only.
        // HSTS tells browsers to always use HTTPS for this domain.
        if (context.Request.IsHttps)
        {
            headers["Strict-Transport-Security"] =
                "max-age=31536000; includeSubDomains; preload";
        }

        // === Content Security Policy (CSP) ===
        // REMOVED: Deprecated X-XSS-Protection header (no longer supported by modern browsers)
        // REMOVED: 'unsafe-inline' from script-src and style-src - this would defeat XSS protection.
        // CSP prevents XSS by controlling which resources can be loaded.
        // NOTE: For production, use nonces or hashes for inline scripts/styles:
        //   script-src 'self' 'nonce-{random}' 
        //   style-src 'self' 'nonce-{random}'
        // Example with nonce middleware:
        //   var nonce = context.Items["CspNonce"] as string;
        //   script-src 'self' 'nonce-{nonce}'
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self'; " +
            "style-src 'self' https://fonts.googleapis.com; " +
            "font-src 'self' https://fonts.gstatic.com; " +
            "img-src 'self' data:; " +
            "connect-src 'self' https://*.his-hope.internal";

        await _next(context);
    }
}
