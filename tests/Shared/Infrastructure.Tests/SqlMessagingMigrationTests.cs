using FluentAssertions;
using His.Hope.Messaging.Sql;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace His.Hope.Infrastructure.Tests;

public sealed class SqlMessagingMigrationTests
{
    [Fact]
    public void SqlMessagingContext_ExposesAnInitialMigration()
    {
        var options = new DbContextOptionsBuilder<SqlMessagingDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=not-used;Username=not-used;Password=not-used",
                npgsql => npgsql.MigrationsAssembly(typeof(SqlMessagingDbContext).Assembly.GetName().Name))
            .Options;
        using var context = new SqlMessagingDbContext(options);

        context.Database.GetMigrations().Should().ContainInOrder(
            "20260901000100_InitialSqlMessaging",
            "20260901170000_AddInboxProcessingLease");
    }
}
