using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.Infrastructure.DataLifecycle;

/// <summary>
/// Applies the database contract shared by every His.Hope write model.
/// The contract is deliberately shadow-property based so bounded contexts do
/// not need to inherit infrastructure types just to get audit metadata.
/// </summary>
public static class HisHopeDataConventions
{
    public const string SoftDeleteAnnotation = "HisHope:SoftDelete";

    public static void Apply(ModelBuilder modelBuilder, params Type[] softDeleteTypes)
    {
        var softDelete = softDeleteTypes.ToHashSet();

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(type => type.ClrType is not null && type.GetTableName() is not null))
        {
            entityType.SetTableName(ToSnakeCase(entityType.GetTableName()!));

            var table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());
            foreach (var property in entityType.GetProperties())
            {
                var currentColumn = property.GetColumnName(table);
                var targetColumn = ToSnakeCase(property.Name);
                // Preserve intentional domain-specific names such as patient_id
                // and appointment_id; normalize legacy Pascal/collapsed names.
                if (currentColumn is null || (!currentColumn.Contains('_') && currentColumn != targetColumn))
                    property.SetColumnName(targetColumn, table);
            }

            var createdAt = entityType.FindProperty("CreatedAt") ?? entityType.AddProperty("CreatedAt", typeof(DateTime?));
            createdAt.SetColumnName("created_at");
            createdAt.SetDefaultValueSql("CURRENT_TIMESTAMP");
            ConfigureString(entityType, "CreatedBy", "created_by");
            ConfigureOptionalDate(entityType, "UpdatedAt", "updated_at");
            ConfigureString(entityType, "UpdatedBy", "updated_by");
            if (entityType.FindProperty("IsDeleted") is null)
                entityType.AddProperty("IsDeleted", typeof(bool?));
            var isDeletedProperty = entityType.FindProperty("IsDeleted")!;
            isDeletedProperty.SetColumnName("is_deleted");
            isDeletedProperty.SetDefaultValue(false);
            ConfigureOptionalDate(entityType, "DeletedAt", "deleted_at");
            ConfigureString(entityType, "DeletedBy", "deleted_by");

            if (softDelete.Contains(entityType.ClrType!))
            {
                entityType.SetAnnotation(SoftDeleteAnnotation, true);
                var entity = modelBuilder.Entity(entityType.ClrType!);
                var parameter = Expression.Parameter(entityType.ClrType!, "entity");
                var isDeleted = Expression.Call(
                    typeof(EF), nameof(EF.Property), [typeof(bool?)],
                    parameter, Expression.Constant("IsDeleted"));
                entity.HasQueryFilter(Expression.Lambda(
                    Expression.NotEqual(isDeleted, Expression.Constant(true, typeof(bool?))), parameter));
            }
        }
    }

    private static void ConfigureOptionalDate(IMutableEntityType entityType, string name, string column)
    {
        if (entityType.FindProperty(name) is null)
            entityType.AddProperty(name, typeof(DateTime?));
        entityType.FindProperty(name)!.SetColumnName(column);
    }

    private static void ConfigureString(IMutableEntityType entityType, string name, string column)
    {
        if (entityType.FindProperty(name) is null)
            entityType.AddProperty(name, typeof(string));
        var property = entityType.FindProperty(name)!;
        property.SetColumnName(column);
        property.SetMaxLength(256);
    }

    private static string ToSnakeCase(string value)
    {
        var snake = Regex.Replace(value, "([a-z0-9])([A-Z])", "$1_$2");
        snake = Regex.Replace(snake, "([A-Z]+)([A-Z][a-z])", "$1_$2");
        return snake.Replace("-", "_").ToLowerInvariant();
    }

}
