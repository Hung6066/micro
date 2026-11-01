using FluentAssertions;
using His.Hope.Persistence;
using Xunit;

namespace His.Hope.Infrastructure.Tests;

public sealed class MigrationRunnerTests
{
    [Fact]
    public async Task Composite_runner_executes_each_database_owner_in_registration_order()
    {
        var calls = new List<string>();
        var runner = new CompositeMigrationRunner(
        [
            new RecordingRunner("service", calls),
            new RecordingRunner("messaging", calls)
        ]);

        await runner.MigrateAsync();

        calls.Should().Equal("service", "messaging");
    }

    private sealed class RecordingRunner(string name, ICollection<string> calls) : IDbMigrationRunner
    {
        public Task MigrateAsync(CancellationToken cancellationToken = default)
        {
            calls.Add(name);
            return Task.CompletedTask;
        }
    }
}
