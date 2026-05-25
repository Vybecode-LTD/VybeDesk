using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using Microsoft.Data.Sqlite;

namespace ClaudePM.Services.Storage;

/// <summary>SQLite-backed implementation of <see cref="IProjectStore"/>.</summary>
public sealed class SqliteProjectStore : IProjectStore
{
    private readonly Database _db;

    public SqliteProjectStore(Database db) => _db = db;

    public event Action? Changed;

    public Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default)
        => _db.ReadAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "SELECT id, name, description, folder_path, status, last_activity, " +
                "model, default_output_path, logo_path " +
                "FROM projects ORDER BY last_activity DESC;";

            var list = new List<Project>();
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                list.Add(new Project
                {
                    Id = Guid.Parse(r.GetString(0)),
                    Name = r.GetString(1),
                    Description = r.GetString(2),
                    FolderPath = r.GetString(3),
                    Status = (ProjectStatus)r.GetInt32(4),
                    LastActivity = DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(5)),
                    Model = r.IsDBNull(6) ? null : r.GetString(6),
                    DefaultOutputPath = r.IsDBNull(7) ? null : r.GetString(7),
                    LogoPath = r.IsDBNull(8) ? null : r.GetString(8),
                });
            }
            return (IReadOnlyList<Project>)list;
        });

    public async Task AddAsync(Project p, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "INSERT INTO projects (id, name, description, folder_path, status, last_activity, " +
                "model, default_output_path, logo_path) " +
                "VALUES ($id, $name, $desc, $path, $status, $la, $model, $outPath, $logo);";
            Bind(cmd, p);
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }

    public async Task UpdateAsync(Project p, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "UPDATE projects SET name=$name, description=$desc, folder_path=$path, " +
                "status=$status, last_activity=$la, model=$model, " +
                "default_output_path=$outPath, logo_path=$logo WHERE id=$id;";
            Bind(cmd, p);
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM projects WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", id.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }

    private static void Bind(SqliteCommand cmd, Project p)
    {
        cmd.Parameters.AddWithValue("$id", p.Id.ToString());
        cmd.Parameters.AddWithValue("$name", p.Name);
        cmd.Parameters.AddWithValue("$desc", p.Description);
        cmd.Parameters.AddWithValue("$path", p.FolderPath);
        cmd.Parameters.AddWithValue("$status", (int)p.Status);
        cmd.Parameters.AddWithValue("$la", p.LastActivity.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$model", (object?)p.Model ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$outPath", (object?)p.DefaultOutputPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$logo", (object?)p.LogoPath ?? DBNull.Value);
    }
}
