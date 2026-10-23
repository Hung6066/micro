using Microsoft.EntityFrameworkCore;

namespace His.Hope.Messaging.Sql;

internal static class SqlExceptionClassifier
{
    private const string PostgreSqlUniqueViolation = "23505";

    public static bool IsUniqueViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.Data.Contains("SqlState") &&
                string.Equals(current.Data["SqlState"]?.ToString(), PostgreSqlUniqueViolation, StringComparison.Ordinal))
            {
                return true;
            }

            if (current.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("duplicate entry", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
