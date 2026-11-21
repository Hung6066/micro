namespace His.Hope.Infrastructure.ObjectStorage;

internal static class ObjectStorageKey
{
    public static string NormalizePrefix(string prefix)
    {
        var normalized = prefix.Trim().Trim('/');
        if (normalized.Length == 0) return "";
        Validate(normalized, nameof(prefix));
        return $"{normalized}/";
    }

    public static string NormalizeKey(string key)
    {
        var normalized = key.Trim();
        Validate(normalized, nameof(key));
        return normalized;
    }

    private static void Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Contains('\\')
            || value.Split('/').Any(segment => segment is "" or "." or "..")
            || value.Any(char.IsControl))
            throw new ArgumentException("Object storage keys must be relative paths without traversal segments.", parameterName);
    }
}
