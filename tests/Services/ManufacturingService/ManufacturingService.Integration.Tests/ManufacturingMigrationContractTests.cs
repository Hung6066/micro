using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

public sealed class ManufacturingMigrationContractTests
{
    [Fact]
    public void Discovers_the_current_manufacturing_lifecycle_migrations()
    {
        var options = new DbContextOptionsBuilder<ManufacturingDbContext>()
            .UseNpgsql("Host=localhost;Database=manufacturing-migration-contract;Username=test;Password=test", npgsql =>
                npgsql.MigrationsAssembly(typeof(ManufacturingDbContext).Assembly.GetName().Name))
            .Options;

        using var db = new ManufacturingDbContext(options);
        var migrations = db.Database.GetMigrations().ToArray();
        migrations.Should().Contain("20260827073000_StandardizeDataLifecycle");
        migrations.Should().Contain("20260828100000_RepairDataLifecycleColumns");
    }

    [Fact]
    public void Every_manufacturing_table_has_snake_case_lifecycle_columns()
    {
        var options = new DbContextOptionsBuilder<ManufacturingDbContext>()
            .UseNpgsql("Host=localhost;Database=manufacturing-model-contract;Username=test;Password=test")
            .Options;

        using var db = new ManufacturingDbContext(options);
        var entities = db.Model.GetEntityTypes()
            .Where(entity => entity.GetTableName() is not null)
            .ToArray();

        entities.Should().HaveCount(52);

        foreach (var entity in entities)
        {
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            entity.GetTableName()!.Should().MatchRegex("^[a-z0-9_]+$");

            var columns = entity.GetProperties()
                .Select(property => property.GetColumnName(table)!)
                .ToArray();

            columns.Should().OnlyContain(column => column == column.ToLowerInvariant() && !column.Contains('-'));
            columns.Should().Contain(new[]
            {
                "created_at", "created_by", "updated_at", "updated_by",
                "is_deleted", "deleted_at", "deleted_by"
            });
        }
    }
}
