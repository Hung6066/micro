using Microsoft.EntityFrameworkCore;
using His.Hope.Messaging.Sql;

namespace His.Hope.Infrastructure.Tests;

public sealed class SqlExceptionClassifierTests
{
    [Fact]
    public void Unique_constraint_violation_is_classified_as_duplicate_delivery()
    {
        var providerException = new Exception("duplicate key value violates unique constraint");
        providerException.Data["SqlState"] = "23505";
        var updateException = new DbUpdateException("insert failed", providerException);

        SqlExceptionClassifier.IsUniqueViolation(updateException).Should().BeTrue();
    }

    [Fact]
    public void Infrastructure_failure_is_not_classified_as_duplicate_delivery()
    {
        var updateException = new DbUpdateException(
            "insert failed",
            new TimeoutException("database connection timed out"));

        SqlExceptionClassifier.IsUniqueViolation(updateException).Should().BeFalse();
    }
}
