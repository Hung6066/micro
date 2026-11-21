using His.Hope.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.ContentService.Infrastructure;

public interface IContentDbContextFactory : IHisHopeDbContextFactory<ContentDbContext>;

internal sealed class ContentDbContextFactoryBridge(
    IHisHopeDbContextFactory<ContentDbContext> inner) : IContentDbContextFactory
{
    public ContentDbContext CreateDbContext(string? tenantKey = null) =>
        inner.CreateDbContext(tenantKey);

    public Task<ContentDbContext> CreateDbContextAsync(
        string? tenantKey = null,
        CancellationToken cancellationToken = default) =>
        inner.CreateDbContextAsync(tenantKey, cancellationToken);

    public ContentDbContext CreateDbContextForConnection(string connectionName) =>
        inner.CreateDbContextForConnection(connectionName);

    public Task<ContentDbContext> CreateDbContextForConnectionAsync(
        string connectionName,
        CancellationToken cancellationToken = default) =>
        inner.CreateDbContextForConnectionAsync(connectionName, cancellationToken);

    public IReadOnlyList<string> GetRegisteredConnectionNames() =>
        inner.GetRegisteredConnectionNames();
}
