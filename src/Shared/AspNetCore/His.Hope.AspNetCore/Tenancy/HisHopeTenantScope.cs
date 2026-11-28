namespace His.Hope.AspNetCore.Tenancy;

public static class HisHopeTenantScope
{
    private static readonly AsyncLocal<string?> CurrentTenant = new();

    public static string? Current => CurrentTenant.Value;

    public static IDisposable Begin(string? tenantKey)
    {
        var previous = CurrentTenant.Value;
        CurrentTenant.Value = string.IsNullOrWhiteSpace(tenantKey) ? null : tenantKey.Trim();
        return new Scope(previous);
    }

    private sealed class Scope(string? previous) : IDisposable
    {
        public void Dispose() => CurrentTenant.Value = previous;
    }
}
