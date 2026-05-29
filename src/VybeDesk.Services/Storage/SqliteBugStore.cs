using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using Microsoft.Data.Sqlite;

namespace VybeDesk.Services.Storage;

/// <summary>
/// SQLite-backed implementation of <see cref="IBugStore"/>. Mirrors
/// <see cref="SqliteProjectStore"/>'s shape: explicit column mapping, Guids
/// as TEXT, enums as INTEGER, Unix-second timestamps. Single-statement
/// writes so no explicit transactions are needed.
/// </summary>
public sealed class SqliteBugStore : IBugStore
{
    private readonly Database _db;

    public SqliteBugStore(Database db) => _db = db;

    public event Action? Changed;

    public Task<IReadOnlyList<Bug>> GetByProjectAsync(Guid projectId, CancellationToken ct = default)
        => _db.ReadAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "SELECT id, project_id, title, severity, status, steps_to_reproduce, " +
                "expected_result, actual_result, area, created " +
                "FROM bugs WHERE project_id=$pid ORDER BY created DESC;";
            cmd.Parameters.AddWithValue("$pid", projectId.ToString());

            var list = new List<Bug>();
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                list.Add(new Bug
                {
                    Id = Guid.Parse(r.GetString(0)),
                    ProjectId = Guid.Parse(r.GetString(1)),
                    Title = r.GetString(2),
                    Severity = (BugSeverity)r.GetInt32(3),
                    Status = (BugStatus)r.GetInt32(4),
                    StepsToReproduce = r.GetString(5),
                    ExpectedResult = r.GetString(6),
                    ActualResult = r.GetString(7),
                    Area = r.GetString(8),
                    Created = DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(9)),
                });
            }
            return (IReadOnlyList<Bug>)list;
        });

    public async Task AddAsync(Bug b, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "INSERT INTO bugs (id, project_id, title, severity, status, " +
                "steps_to_reproduce, expected_result, actual_result, area, created) " +
                "VALUES ($id, $pid, $title, $sev, $st, $steps, $exp, $act, $area, $cr);";
            Bind(cmd, b);
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }

    public async Task UpdateAsync(Bug b, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "UPDATE bugs SET title=$title, severity=$sev, status=$st, " +
                "steps_to_reproduce=$steps, expected_result=$exp, " +
                "actual_result=$act, area=$area WHERE id=$id;";
            Bind(cmd, b);
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM bugs WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", id.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }

    private static void Bind(SqliteCommand cmd, Bug b)
    {
        cmd.Parameters.AddWithValue("$id", b.Id.ToString());
        cmd.Parameters.AddWithValue("$pid", b.ProjectId.ToString());
        cmd.Parameters.AddWithValue("$title", b.Title);
        cmd.Parameters.AddWithValue("$sev", (int)b.Severity);
        cmd.Parameters.AddWithValue("$st", (int)b.Status);
        cmd.Parameters.AddWithValue("$steps", b.StepsToReproduce);
        cmd.Parameters.AddWithValue("$exp", b.ExpectedResult);
        cmd.Parameters.AddWithValue("$act", b.ActualResult);
        cmd.Parameters.AddWithValue("$area", b.Area);
        cmd.Parameters.AddWithValue("$cr", b.Created.ToUnixTimeSeconds());
    }
}
