using System.Text.RegularExpressions;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using Microsoft.Data.Sqlite;

namespace VybeDesk.Services.Storage;

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
                "SELECT id, title, body, category, tags, usage_count, is_favorite, created, modified, project_id " +
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
                    ProjectId = r.IsDBNull(9) ? null : Guid.Parse(r.GetString(9)),
                });
            }
            return (IReadOnlyList<PromptEntry>)list;
        });

    public Task AddAsync(PromptEntry p, CancellationToken ct = default)
        => _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "INSERT INTO prompts (id, title, body, category, tags, usage_count, is_favorite, created, modified, project_id) " +
                "VALUES ($id, $title, $body, $cat, $tags, $usage, $fav, $created, $modified, $pid);";
            Bind(cmd, p);
            await cmd.ExecuteNonQueryAsync(ct);
        });

    public Task UpdateAsync(PromptEntry p, CancellationToken ct = default)
        => _db.WriteAsync(async c =>
        {
            using var tx = c.BeginTransaction();

            // Snapshot the *current* row into prompt_versions, but only if the
            // content (title / body / category / tags) actually changed. This
            // skips usage-count-only updates (e.g. BuildFilledAsync) so the
            // history isn't flooded with no-op entries.
            using (var snapshot = c.CreateCommand())
            {
                snapshot.Transaction = tx;
                snapshot.CommandText =
                    "INSERT INTO prompt_versions " +
                    "  (id, prompt_id, title, body, category, tags, captured) " +
                    "SELECT $vid, id, title, body, category, tags, $captured " +
                    "FROM prompts " +
                    "WHERE id = $id " +
                    "  AND (title != $title OR body != $body " +
                    "       OR category != $cat OR tags != $tags);";
                snapshot.Parameters.AddWithValue("$vid", Guid.NewGuid().ToString());
                snapshot.Parameters.AddWithValue("$captured", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                snapshot.Parameters.AddWithValue("$id", p.Id.ToString());
                snapshot.Parameters.AddWithValue("$title", p.Title);
                snapshot.Parameters.AddWithValue("$body", p.Body);
                snapshot.Parameters.AddWithValue("$cat", p.Category);
                snapshot.Parameters.AddWithValue("$tags", TagSerializer.Serialize(p.Tags));
                await snapshot.ExecuteNonQueryAsync(ct);
            }

            using (var cmd = c.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText =
                    "UPDATE prompts SET title=$title, body=$body, category=$cat, tags=$tags, " +
                    "usage_count=$usage, is_favorite=$fav, modified=$modified, project_id=$pid WHERE id=$id;";
                Bind(cmd, p);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            tx.Commit();
        });

    public Task RemoveAsync(Guid id, CancellationToken ct = default)
        => _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM prompts WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", id.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        });

    public Task<IReadOnlyList<PromptEntry>> SearchAsync(string query, CancellationToken ct = default)
    {
        var ftsQuery = BuildFtsQuery(query);
        if (ftsQuery.Length == 0) return GetAllAsync(ct);

        return _db.ReadAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "SELECT p.id, p.title, p.body, p.category, p.tags, p.usage_count, " +
                "       p.is_favorite, p.created, p.modified, p.project_id " +
                "FROM prompts AS p " +
                "JOIN prompts_fts AS f ON p.rowid = f.rowid " +
                "WHERE prompts_fts MATCH $q " +
                "ORDER BY rank;";
            cmd.Parameters.AddWithValue("$q", ftsQuery);

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
                    ProjectId = r.IsDBNull(9) ? null : Guid.Parse(r.GetString(9)),
                });
            }
            return (IReadOnlyList<PromptEntry>)list;
        });
    }

    public Task<IReadOnlyList<PromptVersion>> GetVersionsAsync(Guid promptId, CancellationToken ct = default)
        => _db.ReadAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "SELECT id, prompt_id, title, body, category, tags, captured " +
                "FROM prompt_versions " +
                "WHERE prompt_id = $id " +
                "ORDER BY captured DESC;";
            cmd.Parameters.AddWithValue("$id", promptId.ToString());

            var list = new List<PromptVersion>();
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                list.Add(new PromptVersion
                {
                    Id = Guid.Parse(r.GetString(0)),
                    PromptId = Guid.Parse(r.GetString(1)),
                    Title = r.GetString(2),
                    Body = r.GetString(3),
                    Category = r.GetString(4),
                    Tags = TagSerializer.Deserialize(r.GetString(5)),
                    Captured = DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(6)),
                });
            }
            return (IReadOnlyList<PromptVersion>)list;
        });

    private static readonly Regex TokenRegex = new(@"[\p{L}\p{N}_]+", RegexOptions.Compiled);

    /// <summary>
    /// Sanitize a user-typed string into an FTS5 query. We extract word
    /// tokens, double-quote each (to neutralize FTS5 operators) and append *
    /// for prefix match. Multiple tokens AND by default in FTS5. Returns ""
    /// when nothing usable remains, so callers can short-circuit to GetAll.
    /// </summary>
    private static string BuildFtsQuery(string user)
    {
        if (string.IsNullOrWhiteSpace(user)) return "";
        var tokens = TokenRegex.Matches(user)
            .Select(m => m.Value)
            .Where(t => t.Length > 0)
            .Select(t => "\"" + t.Replace("\"", "\"\"") + "\"*");
        return string.Join(" ", tokens);
    }

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
        cmd.Parameters.AddWithValue("$pid", p.ProjectId.HasValue ? p.ProjectId.Value.ToString() : (object)DBNull.Value);
    }
}
