using His.Hope.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.CommerceService.Infrastructure.Persistence;

public interface ICommerceDbContextFactory : IHisHopeDbContextFactory<CommerceDbContext>;

internal sealed class CommerceDbContextFactoryBridge(
    IHisHopeDbContextFactory<CommerceDbContext> inner) : ICommerceDbContextFactory
{
    public CommerceDbContext CreateDbContext(string? tenantKey = null) =>
        inner.CreateDbContext(tenantKey);

    public Task<CommerceDbContext> CreateDbContextAsync(
        string? tenantKey = null,
        CancellationToken cancellationToken = default) =>
        inner.CreateDbContextAsync(tenantKey, cancellationToken);

    public CommerceDbContext CreateDbContextForConnection(string connectionName) =>
        inner.CreateDbContextForConnection(connectionName);

    public Task<CommerceDbContext> CreateDbContextForConnectionAsync(
        string connectionName,
        CancellationToken cancellationToken = default) =>
        inner.CreateDbContextForConnectionAsync(connectionName, cancellationToken);

    public IReadOnlyList<string> GetRegisteredConnectionNames() =>
        inner.GetRegisteredConnectionNames();
}
