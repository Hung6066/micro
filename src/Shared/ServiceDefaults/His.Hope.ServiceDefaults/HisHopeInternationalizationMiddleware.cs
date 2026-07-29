using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace His.Hope.ServiceDefaults;

public sealed class HisHopeInternationalizationMiddleware(
    RequestDelegate next,
    IOptions<HisHopeInternationalizationOptions> options)
{
    private readonly HisHopeInternationalizationOptions settings = options.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        var culture = ResolveCulture(context.Request.Headers["Accept-Language"].ToString());
        var timeZone = ResolveTimeZone(context.Request.Headers[HisHopeInternationalizationHeaders.TimeZone].ToString());
        var currency = ResolveCurrency(context.Request.Headers[HisHopeInternationalizationHeaders.Currency].ToString());

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        context.Response.Headers["Content-Language"] = culture.Name;
        context.Response.Headers[HisHopeInternationalizationHeaders.TimeZone] = timeZone;
        context.Response.Headers[HisHopeInternationalizationHeaders.Currency] = currency;
        await next(context);
    }

    private CultureInfo ResolveCulture(string acceptLanguage)
    {
        foreach (var candidate in acceptLanguage.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var language = candidate.Split(';', 2)[0].Trim();
            if (language.Equals("en", StringComparison.OrdinalIgnoreCase)) language = "en-US";
            if (settings.SupportedCultures.Any(s => s.Equals(language, StringComparison.OrdinalIgnoreCase)))
                return CultureInfo.GetCultureInfo(language);
        }
        return CultureInfo.GetCultureInfo(settings.DefaultCulture);
    }

    private string ResolveTimeZone(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return settings.DefaultTimeZone;
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(value); return value; }
        catch (TimeZoneNotFoundException) { return settings.DefaultTimeZone; }
        catch (InvalidTimeZoneException) { return settings.DefaultTimeZone; }
    }

    private string ResolveCurrency(string value) =>
        value.Length == 3 && value.All(char.IsLetter) ? value.ToUpperInvariant() : settings.DefaultCurrency;
}
