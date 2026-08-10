using System.Text.Json;
using System.Net.Sockets;
using Npgsql;

namespace His.Hope.DatabaseContinuityService;

public sealed record ContinuityAuditEntry(
    string JobId, string Operation, string TargetEnvironment, string Status, string ActorSubject,
    string CorrelationId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string? ErrorCode, string? ResultJson);

public sealed class ContinuityAuditStore(IConfiguration configuration)
{
    private readonly string _connectionString = ResolveConnectionString(configuration);

    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var configured = configuration["DatabaseContinuity:AuditConnectionString"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        configured = configuration.GetConnectionString("ContinuityAudit");
        return !string.IsNullOrWhiteSpace(configured)
            ? configured
            : BuildConnectionStringFromEnvironment(configuration) ?? "";
    }

    private static string? BuildConnectionStringFromEnvironment(IConfiguration configuration)
    {
        var host = configuration["PGHOST"];
        var port = configuration["PGPORT"] ?? "5432";
        var user = configuration["PGUSER"];
        var password = configuration["PGPASSWORD"];
        var database = configuration["PGDATABASE"] ?? "identitydb";
        return string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password)
            ? null
            : $"Host={host};Port={port};Database={database};Username={user};Password={password}";
    }

    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var connection = await OpenAsync(ct);
                await using var command = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS his_hope_database_continuity_audit (
                job_id text PRIMARY KEY,
                operation text NOT NULL,
                target_environment text NOT NULL,
                status text NOT NULL,
                actor_subject text NOT NULL,
                correlation_id text NOT NULL,
                created_at timestamptz NOT NULL,
                updated_at timestamptz NOT NULL,
                error_code text NULL,
                result_json jsonb NULL
            );
            CREATE INDEX IF NOT EXISTS ix_hh_continuity_audit_updated_at
                ON his_hope_database_continuity_audit (updated_at DESC);
            """, connection);
                await command.ExecuteNonQueryAsync(ct);
                return;
            }
            catch (NpgsqlException) when (attempt < 8)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt, 5)), ct);
            }
            catch (SocketException) when (attempt < 8)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt, 5)), ct);
            }
        }
    }

    public async Task UpsertAsync(ContinuityJob job, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = new NpgsqlCommand("""
            INSERT INTO his_hope_database_continuity_audit
                (job_id, operation, target_environment, status, actor_subject, correlation_id, created_at, updated_at, error_code, result_json)
            VALUES (@job_id, @operation, @target_environment, @status, @actor_subject, @correlation_id, @created_at, @updated_at, @error_code, @result_json::jsonb)
            ON CONFLICT (job_id) DO UPDATE SET
                status = EXCLUDED.status, updated_at = EXCLUDED.updated_at,
                error_code = EXCLUDED.error_code, result_json = EXCLUDED.result_json;
            """, connection);
        AddParameters(command, job);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<ContinuityAuditEntry>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT job_id, operation, target_environment, status, actor_subject, correlation_id,
                   created_at, updated_at, error_code, result_json
            FROM his_hope_database_continuity_audit
            ORDER BY updated_at DESC
            OFFSET @offset LIMIT @limit;
            """, connection);
        command.Parameters.AddWithValue("offset", Math.Max(0, page - 1) * Math.Clamp(pageSize, 1, 100));
        command.Parameters.AddWithValue("limit", Math.Clamp(pageSize, 1, 100));
        var entries = new List<ContinuityAuditEntry>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            entries.Add(new(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6), reader.GetFieldValue<DateTimeOffset>(7),
                reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9)));
        }
        return entries;
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException("Database continuity audit connection is not configured.");
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private static void AddParameters(NpgsqlCommand command, ContinuityJob job)
    {
        command.Parameters.AddWithValue("job_id", job.JobId);
        command.Parameters.AddWithValue("operation", job.Operation);
        command.Parameters.AddWithValue("target_environment", job.TargetEnvironment);
        command.Parameters.AddWithValue("status", job.Status.ToString());
        command.Parameters.AddWithValue("actor_subject", job.ActorSubject);
        command.Parameters.AddWithValue("correlation_id", job.CorrelationId);
        command.Parameters.AddWithValue("created_at", job.CreatedAt);
        command.Parameters.AddWithValue("updated_at", job.UpdatedAt);
        command.Parameters.AddWithValue("error_code", (object?)job.ErrorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("result_json", (object?)job.ResultJson ?? DBNull.Value);
    }
}
