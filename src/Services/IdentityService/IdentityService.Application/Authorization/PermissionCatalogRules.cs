using His.Hope.SharedKernel.Authorization;

namespace His.Hope.IdentityService.Application.Authorization;

public static class PermissionCatalogRules
{
    public static bool IsValid(string? code, IEnumerable<string> registeredPrefixes)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        if (HisHopePermissions.IsValid(code)) return true;

        var parts = code.Split('.', StringSplitOptions.None);
        if (parts.Length < 2 || parts.Any(part => part.Length == 0 || part.Any(character =>
                !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))))
            return false;

        return registeredPrefixes.Any(prefix =>
            string.Equals(prefix, parts[0], StringComparison.OrdinalIgnoreCase));
    }
}