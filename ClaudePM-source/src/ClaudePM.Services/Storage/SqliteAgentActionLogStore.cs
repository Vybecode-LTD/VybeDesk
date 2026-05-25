using System.Globalization;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;

namespace ClaudePM.Services.Storage;

/// <summary>
/// SQLite-backed implementation of <see cref="IAgentActionLogStore"/>.
/// Per-project newest-first log of executed agent actions; mirrors the
/// shape of <see cref="SqliteAuditHistoryStore"/> deliberately so the two
/// per-project history surfaces behave identically.
///
/// <c>executed_at</c> is stored as an ISO-8601 string so the timestamp
/// round-trips with full <see cref="DateTimeOffset"/> precision (including
/// offset), which matters when reconstructing the original execute context
/// of an old action across timezone changes.
/// </summary>
public sealed class SqliteAgentActionLogStore : IAgentActionLogStore
{
    private readonly Database _db;

    public SqliteAgentActionLogStore(Database db) => _db = db;

    public event Action? Changed;

    public async Task AddAsync(AgentActionLogEntry entry, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO agent_actions
                    (id, project_id, kind, path, destination_path,
                     original_content, status, executed_at, description, new_content)
                VALUES ($id, $pid, $kind, $path, $dest,
                        $orig, $status, $exec, $desc, $new);";
            cmd.Parameters.AddWithValue("$id", entry.Id.ToString());
            cmd.Parameters.AddWithValue("$pid", entry.ProjectId.ToString());
            cmd.Parameters.AddWithValue("$kind", (int)entry.Kind);
            cmd.Parameters.AddWithValue("$path", entry.Path);
            cmd.Parameters.AddWithValue("$dest", entry.DestinationPath);
            cmd.Parameters.AddWithValue("$orig",
                (object?)entry.OriginalContent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$status", (int)entry.Status);
            cmd.Parameters.AddWithValue("$exec",
                entry.ExecutedAt.ToString("O", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$desc", entry.Description);
            cmd.Parameters.AddWithValue("$new",
                (object?)entry.NewContent ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }

    public Task<IReadOnlyList<AgentActionLogEntry>> GetByProjectAsync(
        Guid projectId, int limit = 50, CancellationToken ct = default)
        => _db.ReadAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "SELECT id, project_id, kind, path, destination_path, " +
                "original_content, status, executed_at, description, new_content " +
                "FROM agent_actions WHERE project_id=$pid " +
                "ORDER BY executed_at DESC LIMIT $limit;";
            cmd.Parameters.AddWithValue("$pid", projectId.ToString());
            cmd.Parameters.AddWithValue("$limit", limit);

            var list = new List<AgentActionLogEntry>();
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add(Read(r));
            return (IReadOnlyList<AgentActionLogEntry>)list;
        });

    public Task<AgentActionLogEntry?> GetMostRecentUndoableAsync(
        Guid projectId, CancellationToken ct = default)
        => _db.ReadAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "SELECT id, project_id, kind, path, destination_path, " +
                "original_content, status, executed_at, description, new_content " +
                "FROM agent_actions WHERE project_id=$pid AND status=$done " +
                "ORDER BY executed_at DESC LIMIT 1;";
            cmd.Parameters.AddWithValue("$pid", projectId.ToString());
            cmd.Parameters.AddWithValue("$done", (int)AgentActionLogStatus.Done);

            using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;
            return (AgentActionLogEntry?)Read(r);
        });

    public Task<AgentActionLogEntry?> GetMostRecentUndoneAsync(
        Guid projectId, CancellationToken ct = default)
        => _db.ReadAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "SELECT id, project_id, kind, path, destination_path, " +
                "original_content, status, executed_at, description, new_content " +
                "FROM agent_actions WHERE project_id=$pid AND status=$undone " +
                "ORDER BY executed_at DESC LIMIT 1;";
            cmd.Parameters.AddWithValue("$pid", projectId.ToString());
            cmd.Parameters.AddWithValue("$undone", (int)AgentActionLogStatus.Undone);

            using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;
            return (AgentActionLogEntry?)Read(r);
        });

    public async Task UpdateStatusAsync(Guid id, AgentActionLogStatus status, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE agent_actions SET status=$status WHERE id=$id;";
            cmd.Parameters.AddWithValue("$status", (int)status);
            cmd.Parameters.AddWithValue("$id", id.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM agent_actions WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", id.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }

    public async Task ClearProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM agent_actions WHERE project_id=$pid;";
            cmd.Parameters.AddWithValue("$pid", projectId.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }

    private static AgentActionLogEntry Read(Microsoft.Data.Sqlite.SqliteDataReader r)
        => new()
        {
            Id = Guid.Parse(r.GetString(0)),
            ProjectId = Guid.Parse(r.GetString(1)),
            Kind = (AgentActionKind)r.GetInt32(2),
            Path = r.GetString(3),
            DestinationPath = r.GetString(4),
            OriginalContent = r.IsDBNull(5) ? null : r.GetString(5),
            Status = (AgentActionLogStatus)r.GetInt32(6),
            ExecutedAt = DateTimeOffset.Parse(r.GetString(7),
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            Description = r.GetString(8),
            NewContent = r.IsDBNull(9) ? null : r.GetString(9),
        };
}
