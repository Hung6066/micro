namespace His.Hope.ServiceDefaults;

public sealed class HisHopeInternationalizationOptions
{
    public const string SectionName = "Internationalization";
    public string DefaultCulture { get; set; } = "vi-VN";
    public string DefaultTimeZone { get; set; } = "Asia/Ho_Chi_Minh";
    public string DefaultCurrency { get; set; } = "VND";
    public string[] SupportedCultures { get; set; } = ["vi-VN", "en-US"];
}

public static class HisHopeInternationalizationHeaders
{
    public const string TimeZone = "X-Timezone";
    public const string Currency = "X-Currency";
}
