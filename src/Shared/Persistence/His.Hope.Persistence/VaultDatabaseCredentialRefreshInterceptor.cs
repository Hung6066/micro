using System.Data.Common;
using His.Hope.Secrets;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace His.Hope.Persistence;

/// <summary>
/// Refreshes leased Vault database credentials before EF Core opens a connection.
/// </summary>
public sealed class VaultDatabaseCredentialRefreshInterceptor(
    IVaultDatabaseConnectionStringResolver resolver,
    string configuredConnectionString,
    string connectionName) : DbConnectionInterceptor
{
    public override InterceptionResult ConnectionOpening(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        RefreshConnectionString(connection);
        return result;
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        RefreshConnectionString(connection);
        return ValueTask.FromResult(result);
    }

    private void RefreshConnectionString(DbConnection connection)
    {
        var resolvedConnectionString = resolver.Resolve(configuredConnectionString, connectionName);
        if (!string.Equals(connection.ConnectionString, resolvedConnectionString, StringComparison.Ordinal))
            connection.ConnectionString = resolvedConnectionString;
    }
}
