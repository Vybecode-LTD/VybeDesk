using System.Text.RegularExpressions;
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

    public Task<IReadOnlyList<PromptEntry>> SearchAsync(string query, CancellationToken ct = default)
    {
        var ftsQuery = BuildFtsQuery(query);
        if (ftsQuery.Length == 0) return GetAllAsync(ct);

        return _db.ReadAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "SELECT p.id, p.title, p.body, p.category, p.tags, p.usage_count, " +
                "       p.is_favorite, p.created, p.modified " +
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
                });
            }
            return (IReadOnlyList<PromptEntry>)list;
        });
    }

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
    }
}
