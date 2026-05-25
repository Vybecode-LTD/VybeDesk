using System.Text.Json;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using Microsoft.Data.Sqlite;

namespace ClaudePM.Services.Storage;

/// <summary>
/// SQLite-backed implementation of <see cref="IAuditHistoryStore"/>.
/// Append-only on the user side (each <see cref="AddAsync"/> is a new row);
/// the verdict list is stored as JSON TEXT consistent with how prompt tags
/// and questionnaire answers are stored elsewhere.
/// </summary>
public sealed class SqliteAuditHistoryStore : IAuditHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly Database _db;

    public SqliteAuditHistoryStore(Database db) => _db = db;

    public event Action? Changed;

    public Task<IReadOnlyList<AuditHistoryEntry>> GetByProjectAsync(
        Guid projectId, CancellationToken ct = default)
        => _db.ReadAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "SELECT id, project_id, mode, off_track_count, at_risk_count, " +
                "on_track_count, report_md, deep_dive_md, verdicts_json, generated_at " +
                "FROM audit_history WHERE project_id=$pid " +
                "ORDER BY generated_at DESC;";
            cmd.Parameters.AddWithValue("$pid", projectId.ToString());

            var list = new List<AuditHistoryEntry>();
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                list.Add(new AuditHistoryEntry
                {
                    Id = Guid.Parse(r.GetString(0)),
                    ProjectId = Guid.Parse(r.GetString(1)),
                    Mode = (AuditMode)r.GetInt32(2),
                    OffTrackCount = r.GetInt32(3),
                    AtRiskCount = r.GetInt32(4),
                    OnTrackCount = r.GetInt32(5),
                    ReportMarkdown = r.GetString(6),
                    DeepDivePrompt = r.GetString(7),
                    Verdicts = JsonSerializer.Deserialize<List<StatementVerdict>>(r.GetString(8), JsonOptions)
                               ?? new List<StatementVerdict>(),
                    GeneratedAt = DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(9)),
                });
            }
            return (IReadOnlyList<AuditHistoryEntry>)list;
        });

    public async Task AddAsync(AuditHistoryEntry entry, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO audit_history
                    (id, project_id, mode, off_track_count, at_risk_count,
                     on_track_count, report_md, deep_dive_md, verdicts_json, generated_at)
                VALUES ($id, $pid, $mode, $off, $at, $on, $rep, $deep, $verd, $gen);";
            cmd.Parameters.AddWithValue("$id", entry.Id.ToString());
            cmd.Parameters.AddWithValue("$pid", entry.ProjectId.ToString());
            cmd.Parameters.AddWithValue("$mode", (int)entry.Mode);
            cmd.Parameters.AddWithValue("$off", entry.OffTrackCount);
            cmd.Parameters.AddWithValue("$at", entry.AtRiskCount);
            cmd.Parameters.AddWithValue("$on", entry.OnTrackCount);
            cmd.Parameters.AddWithValue("$rep", entry.ReportMarkdown);
            cmd.Parameters.AddWithValue("$deep", entry.DeepDivePrompt);
            cmd.Parameters.AddWithValue("$verd",
                JsonSerializer.Serialize(entry.Verdicts, JsonOptions));
            cmd.Parameters.AddWithValue("$gen", entry.GeneratedAt.ToUnixTimeSeconds());
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }

    public async Task RemoveAsync(Guid entryId, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM audit_history WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", entryId.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }

    public async Task ClearProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM audit_history WHERE project_id=$pid;";
            cmd.Parameters.AddWithValue("$pid", projectId.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }
}
