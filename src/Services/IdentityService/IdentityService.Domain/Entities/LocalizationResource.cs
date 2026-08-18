namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>Stable machine key for a translatable catalog entry.</summary>
public class LocalizationResource
{
    public string ScopeId { get; set; } = IdentityScope.Global;
    public string Key { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<LocalizationTranslation> Translations { get; set; } = new List<LocalizationTranslation>();
}

public class LocalizationTranslation
{
    public string ScopeId { get; set; } = IdentityScope.Global;
    public string ResourceKey { get; set; } = string.Empty;
    public string Locale { get; set; } = "vi-VN";
    public string Value { get; set; } = string.Empty;
    public LocalizationResource Resource { get; set; } = null!;
}
