using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace His.Hope.Infrastructure.Configuration;

/// <summary>
/// Confirms that an explicit migration run left the complete EF relational model
/// in the target database. Migration history alone is not enough: a previously
/// recorded migration can coexist with a manually dropped table.
/// </summary>
public static class MigrationSchemaVerifier
{
    public static void Verify(DbContext db, string serviceName)
    {
        var pendingMigrations = db.Database.GetPendingMigrations().ToArray();
        if (pendingMigrations.Length > 0)
        {
            throw new InvalidOperationException(
                $"{serviceName} migration completed with {pendingMigrations.Length} pending migration(s).");
        }

        var model = db.Model;
        var defaultSchema = model.GetDefaultSchema() ?? "public";
        var expectedTables = model.GetRelationalModel().Tables
            .Select(table => (Schema: table.Schema ?? defaultSchema, table.Name))
            .Distinct()
            .ToArray();
        if (expectedTables.Length == 0)
        {
            throw new InvalidOperationException($"{serviceName} has no relational tables to verify.");
        }

        var connection = db.Database.GetDbConnection();
        var closeAfterVerification = connection.State != System.Data.ConnectionState.Open;
        if (closeAfterVerification) db.Database.OpenConnection();

        try
        {
            var missingTables = new List<string>();
            foreach (var table in expectedTables)
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT to_regclass(@relation_name) IS NOT NULL";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "relation_name";
                parameter.Value = $"{table.Schema}.{table.Name}";
                command.Parameters.Add(parameter);
                if (!Equals(command.ExecuteScalar(), true))
                    missingTables.Add($"{table.Schema}.{table.Name}");
            }

            if (missingTables.Count > 0)
            {
                var sample = string.Join(", ", missingTables.Take(20));
                var remaining = missingTables.Count > 20 ? $" and {missingTables.Count - 20} more" : string.Empty;
                throw new InvalidOperationException(
                    $"{serviceName} migration completed but {missingTables.Count} required database table(s) are missing: {sample}{remaining}.");
            }
        }
        finally
        {
            if (closeAfterVerification) db.Database.CloseConnection();
        }
    }
}
