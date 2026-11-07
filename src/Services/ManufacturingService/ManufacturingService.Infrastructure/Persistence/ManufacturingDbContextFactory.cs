using His.Hope.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.ManufacturingService.Infrastructure.Persistence;

public interface IManufacturingDbContextFactory : IHisHopeDbContextFactory<ManufacturingDbContext>;

internal sealed class ManufacturingDbContextFactoryBridge(
    IHisHopeDbContextFactory<ManufacturingDbContext> inner) : IManufacturingDbContextFactory
{
    public ManufacturingDbContext CreateDbContext(string? tenantKey = null) =>
        inner.CreateDbContext(tenantKey);

    public Task<ManufacturingDbContext> CreateDbContextAsync(
        string? tenantKey = null,
        CancellationToken cancellationToken = default) =>
        inner.CreateDbContextAsync(tenantKey, cancellationToken);

    public ManufacturingDbContext CreateDbContextForConnection(string connectionName) =>
        inner.CreateDbContextForConnection(connectionName);

    public Task<ManufacturingDbContext> CreateDbContextForConnectionAsync(
        string connectionName,
        CancellationToken cancellationToken = default) =>
        inner.CreateDbContextForConnectionAsync(connectionName, cancellationToken);

    public IReadOnlyList<string> GetRegisteredConnectionNames() =>
        inner.GetRegisteredConnectionNames();
}
