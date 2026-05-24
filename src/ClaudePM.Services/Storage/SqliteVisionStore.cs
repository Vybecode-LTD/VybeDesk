using System.Text.Json;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using Microsoft.Data.Sqlite;

namespace ClaudePM.Services.Storage;

/// <summary>
/// SQLite-backed implementation of <see cref="IVisionStore"/>. One row per
/// project (enforced by the UNIQUE constraint on <c>project_id</c>), so
/// <see cref="SaveAsync"/> is an upsert. Statements stored as JSON TEXT
/// (consistent with how prompt tags and questionnaire answers are stored).
/// </summary>
public sealed class SqliteVisionStore : IVisionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly Database _db;

    public SqliteVisionStore(Database db) => _db = db;

    public event Action? Changed;

    public Task<VisionRecord?> GetByProjectAsync(Guid projectId, CancellationToken ct = default)
        => _db.ReadAsync<VisionRecord?>(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "SELECT id, project_id, statements_json, approved_at, created, modified " +
                "FROM vision_records WHERE project_id=$pid LIMIT 1;";
            cmd.Parameters.AddWithValue("$pid", projectId.ToString());

            using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;

            return new VisionRecord
            {
                Id = Guid.Parse(r.GetString(0)),
                ProjectId = Guid.Parse(r.GetString(1)),
                Statements = JsonSerializer.Deserialize<List<VisionStatement>>(r.GetString(2), JsonOptions)
                             ?? new List<VisionStatement>(),
                ApprovedAt = r.IsDBNull(3)
                    ? (DateTimeOffset?)null
                    : DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(3)),
                Created = DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(4)),
                Modified = DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(5)),
            };
        });

    public async Task SaveAsync(VisionRecord record, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO vision_records
                    (id, project_id, statements_json, approved_at, created, modified)
                VALUES ($id, $pid, $stmts, $appr, $cr, $mod)
                ON CONFLICT(project_id) DO UPDATE SET
                    statements_json = excluded.statements_json,
                    approved_at     = excluded.approved_at,
                    modified        = excluded.modified;";
            Bind(cmd, record);
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }

    public async Task RemoveAsync(Guid projectId, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM vision_records WHERE project_id=$pid;";
            cmd.Parameters.AddWithValue("$pid", projectId.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }

    private static void Bind(SqliteCommand cmd, VisionRecord r)
    {
        cmd.Parameters.AddWithValue("$id", r.Id.ToString());
        cmd.Parameters.AddWithValue("$pid", r.ProjectId.ToString());
        cmd.Parameters.AddWithValue("$stmts",
            JsonSerializer.Serialize(r.Statements, JsonOptions));
        cmd.Parameters.AddWithValue("$appr",
            r.ApprovedAt is null ? DBNull.Value : (object)r.ApprovedAt.Value.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$cr", r.Created.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$mod", r.Modified.ToUnixTimeSeconds());
    }
}
