using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using Microsoft.Data.Sqlite;

namespace VybeDesk.Services.Storage;

public sealed class SqliteAiCallStore : IAiCallStore
{
    private readonly Database _db;

    public event Action? Changed;

    public SqliteAiCallStore(Database db) => _db = db;

    public async Task AddAsync(AiCallRecord r, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "INSERT INTO ai_calls (id, project_id, module, model, input_tokens, output_tokens, " +
                "cache_creation_tokens, cache_read_tokens, cost_estimate, duration_ms, timestamp) " +
                "VALUES ($id, $pid, $mod, $model, $in, $out, $cc, $cr, $cost, $dur, $ts);";
            cmd.Parameters.AddWithValue("$id", r.Id.ToString());
            cmd.Parameters.AddWithValue("$pid", r.ProjectId.HasValue ? r.ProjectId.Value.ToString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$mod", r.Module);
            cmd.Parameters.AddWithValue("$model", r.Model);
            cmd.Parameters.AddWithValue("$in", r.InputTokens);
            cmd.Parameters.AddWithValue("$out", r.OutputTokens);
            cmd.Parameters.AddWithValue("$cc", r.CacheCreationInputTokens);
            cmd.Parameters.AddWithValue("$cr", r.CacheReadInputTokens);
            cmd.Parameters.AddWithValue("$cost", r.CostEstimate);
            cmd.Parameters.AddWithValue("$dur", r.DurationMs);
            cmd.Parameters.AddWithValue("$ts", r.Timestamp.ToUnixTimeSeconds());
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }

    public Task<IReadOnlyList<AiCallRecord>> GetRecentAsync(int limit = 50, CancellationToken ct = default)
        => _db.ReadAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "SELECT id, project_id, module, model, input_tokens, output_tokens, " +
                "cache_creation_tokens, cache_read_tokens, cost_estimate, duration_ms, timestamp " +
                "FROM ai_calls ORDER BY timestamp DESC LIMIT $limit;";
            cmd.Parameters.AddWithValue("$limit", limit);
            return await ReadRecordsAsync(cmd, ct);
        });

    public Task<IReadOnlyList<AiCallRecord>> GetByProjectAsync(Guid projectId, int limit = 50, CancellationToken ct = default)
        => _db.ReadAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "SELECT id, project_id, module, model, input_tokens, output_tokens, " +
                "cache_creation_tokens, cache_read_tokens, cost_estimate, duration_ms, timestamp " +
                "FROM ai_calls WHERE project_id = $pid ORDER BY timestamp DESC LIMIT $limit;";
            cmd.Parameters.AddWithValue("$pid", projectId.ToString());
            cmd.Parameters.AddWithValue("$limit", limit);
            return await ReadRecordsAsync(cmd, ct);
        });

    public Task<AiCallSummary> GetSummaryAsync(CancellationToken ct = default)
        => _db.ReadAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "SELECT COUNT(*), " +
                "COALESCE(SUM(input_tokens), 0), " +
                "COALESCE(SUM(output_tokens), 0), " +
                "COALESCE(SUM(cache_creation_tokens), 0), " +
                "COALESCE(SUM(cache_read_tokens), 0), " +
                "COALESCE(SUM(cost_estimate), 0.0) " +
                "FROM ai_calls;";
            using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
                return new AiCallSummary(0, 0, 0, 0, 0, 0);
            return new AiCallSummary(
                r.GetInt32(0),
                r.GetInt64(1),
                r.GetInt64(2),
                r.GetInt64(3),
                r.GetInt64(4),
                r.GetDouble(5));
        });

    private static async Task<IReadOnlyList<AiCallRecord>> ReadRecordsAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<AiCallRecord>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new AiCallRecord
            {
                Id = Guid.Parse(r.GetString(0)),
                ProjectId = r.IsDBNull(1) ? null : Guid.Parse(r.GetString(1)),
                Module = r.GetString(2),
                Model = r.GetString(3),
                InputTokens = r.GetInt32(4),
                OutputTokens = r.GetInt32(5),
                CacheCreationInputTokens = r.GetInt32(6),
                CacheReadInputTokens = r.GetInt32(7),
                CostEstimate = r.GetDouble(8),
                DurationMs = r.GetInt32(9),
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(10)),
            });
        }
        return list;
    }
}
