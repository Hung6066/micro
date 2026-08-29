using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.Infrastructure.DataLifecycle;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class IdentityDataConventionsTests
{
    [Fact]
    public void Identity_model_uses_snake_case_and_shared_lifecycle_columns()
    {
        using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

        foreach (var entityType in db.Model.GetEntityTypes().Where(type => type.GetTableName() is not null))
        {
            var table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());
            Assert.DoesNotMatch("[A-Z]", entityType.GetTableName()!);
            Assert.Contains(entityType.GetProperties(), property => property.GetColumnName(table) == "created_at");
            Assert.Contains(entityType.GetProperties(), property => property.GetColumnName(table) == "updated_at");
            Assert.Contains(entityType.GetProperties(), property => property.GetColumnName(table) == "created_by");
            Assert.Contains(entityType.GetProperties(), property => property.GetColumnName(table) == "updated_by");
            Assert.Contains(entityType.GetProperties(), property => property.GetColumnName(table) == "is_deleted");
            Assert.Contains(entityType.GetProperties(), property => property.GetColumnName(table) == "deleted_at");
            Assert.Contains(entityType.GetProperties(), property => property.GetColumnName(table) == "deleted_by");

            if (!entityType.GetTableName()!.Contains("outbox", StringComparison.OrdinalIgnoreCase)
                && !entityType.GetTableName()!.Contains("event_receipt", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var property in entityType.GetProperties())
                    Assert.DoesNotMatch("[A-Z]", property.GetColumnName(table)!);
            }
        }
    }

    [Fact]
    public void Marked_identity_entities_are_soft_deleted_and_stamped()
    {
        using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .AddInterceptors(new SoftDeleteInterceptor(() => Guid.NewGuid()))
            .Options);
        var user = new User { UserName = "lifecycle-test", FirstName = "Lifecycle", LastName = "Test" };

        db.Users.Add(user);
        db.SaveChanges();
        db.Users.Remove(user);
        db.SaveChanges();

        Assert.Empty(db.Users.ToList());
        var deleted = db.Users.IgnoreQueryFilters().Single();
        Assert.True(db.Entry(deleted).Property<bool?>("IsDeleted").CurrentValue);
        Assert.NotEqual(default, db.Entry(deleted).Property<DateTime>("CreatedAt").CurrentValue);
        Assert.NotNull(db.Entry(deleted).Property<DateTime?>("DeletedAt").CurrentValue);
        Assert.NotNull(db.Entry(deleted).Property<string>("DeletedBy").CurrentValue);
    }

    [Fact]
    public void User_model_exposes_stable_listing_indexes()
    {
        using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=identitydb;Username=postgres;Password=postgres")
            .Options);

        var user = db.Model.FindEntityType(typeof(User));
        Assert.NotNull(user);
        Assert.Contains(user!.GetIndexes(), index => index.GetDatabaseName() == "ix_asp_net_users_created_at_id");
        Assert.Contains(user.GetIndexes(), index => index.GetDatabaseName() == "ix_asp_net_users_active_created_at_id");
    }
}
