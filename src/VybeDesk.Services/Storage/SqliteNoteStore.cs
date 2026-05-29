using VybeDesk.Core.Models;
using VybeDesk.Core.Services;

namespace VybeDesk.Services.Storage;

/// <summary>SQLite-backed implementation of <see cref="INoteStore"/>.</summary>
public sealed class SqliteNoteStore : INoteStore
{
    private readonly Database _db;

    public event Action? Changed;

    public SqliteNoteStore(Database db) => _db = db;

    public Task<IReadOnlyList<Note>> GetAllAsync(CancellationToken ct = default)
        => _db.ReadAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "SELECT id, title, body, tags, project_id, created " +
                "FROM notes ORDER BY created DESC;";

            var list = new List<Note>();
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                list.Add(new Note
                {
                    Id = Guid.Parse(r.GetString(0)),
                    Title = r.GetString(1),
                    Body = r.GetString(2),
                    Tags = TagSerializer.Deserialize(r.GetString(3)),
                    ProjectId = r.IsDBNull(4) ? null : Guid.Parse(r.GetString(4)),
                    Created = DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(5)),
                });
            }
            return (IReadOnlyList<Note>)list;
        });

    public async Task AddAsync(Note n, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "INSERT INTO notes (id, title, body, tags, project_id, created) " +
                "VALUES ($id, $title, $body, $tags, $pid, $created);";
            cmd.Parameters.AddWithValue("$id", n.Id.ToString());
            cmd.Parameters.AddWithValue("$title", n.Title);
            cmd.Parameters.AddWithValue("$body", n.Body);
            cmd.Parameters.AddWithValue("$tags", TagSerializer.Serialize(n.Tags));
            cmd.Parameters.AddWithValue("$pid", (object?)n.ProjectId?.ToString() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$created", n.Created.ToUnixTimeSeconds());
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM notes WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", id.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }
}
