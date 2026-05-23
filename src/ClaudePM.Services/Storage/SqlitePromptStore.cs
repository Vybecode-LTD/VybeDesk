using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using Microsoft.Data.Sqlite;

namespace ClaudePM.Services.Storage;

/// <summary>SQLite-backed implementation of <see cref="IPromptStore"/>.</summary>
public sealed class SqlitePromptStore : IPromptStore
{
    private readonly Database _db;

    public SqlitePromptStore(Database db) => _db = db;

    public Task<IReadOnlyList<PromptEntry>> GetAllAsync(CancellationToken ct = default)
        => _db.ReadAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "SELECT id, title, body, category, tags, usage_count, is_favorite, created, modified " +
                "FROM prompts ORDER BY modified DESC;";

            var list = new List<PromptEntry>();
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                list.Add(new PromptEntry
                {
                    Id = Guid.Parse(r.GetString(0)),
                    Title = r.GetString(1),
                    Body = r.GetString(2),
                    Category = r.GetString(3),
                    Tags = TagSerializer.Deserialize(r.GetString(4)),
                    UsageCount = r.GetInt32(5),
                    IsFavorite = r.GetInt64(6) != 0,
                    Created = DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(7)),
                    Modified = DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(8)),
                });
            }
            return (IReadOnlyList<PromptEntry>)list;
        });

    public Task AddAsync(PromptEntry p, CancellationToken ct = default)
        => _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "INSERT INTO prompts (id, title, body, category, tags, usage_count, is_favorite, created, modified) " +
                "VALUES ($id, $title, $body, $cat, $tags, $usage, $fav, $created, $modified);";
            Bind(cmd, p);
            await cmd.ExecuteNonQueryAsync(ct);
        });

    public Task UpdateAsync(PromptEntry p, CancellationToken ct = default)
        => _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "UPDATE prompts SET title=$title, body=$body, category=$cat, tags=$tags, " +
                "usage_count=$usage, is_favorite=$fav, modified=$modified WHERE id=$id;";
            Bind(cmd, p);
            await cmd.ExecuteNonQueryAsync(ct);
        });

    public Task RemoveAsync(Guid id, CancellationToken ct = default)
        => _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM prompts WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", id.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        });

    private static void Bind(SqliteCommand cmd, PromptEntry p)
    {
        cmd.Parameters.AddWithValue("$id", p.Id.ToString());
        cmd.Parameters.AddWithValue("$title", p.Title);
        cmd.Parameters.AddWithValue("$body", p.Body);
        cmd.Parameters.AddWithValue("$cat", p.Category);
        cmd.Parameters.AddWithValue("$tags", TagSerializer.Serialize(p.Tags));
        cmd.Parameters.AddWithValue("$usage", p.UsageCount);
        cmd.Parameters.AddWithValue("$fav", p.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("$created", p.Created.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$modified", p.Modified.ToUnixTimeSeconds());
    }
}
