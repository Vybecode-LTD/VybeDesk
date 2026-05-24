using Microsoft.Data.Sqlite;

namespace ClaudePM.Services.Storage;

/// <summary>
/// SQLite connection management and schema initialization for ClaudePM.
/// WAL mode, pooled reader connections, and a single serialized writer — per
/// embedded-SQLite guidance. The database file lives at
/// %LOCALAPPDATA%\ClaudePM\claudepm.db.
/// </summary>
public sealed class Database : IDisposable
{
    private readonly string _connStr;
    private readonly SemaphoreSlim _writerLock = new(1, 1);

    public Database() : this(Path.Combine(Paths.AppDataDir(), "claudepm.db")) { }

    public Database(string path)
    {
        _connStr = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
        }.ToString();

        using var c = Open();
        Migrate(c);
        Seed(c);
    }

    /// <summary>Opens a connection and applies the standard pragmas.</summary>
    public SqliteConnection Open()
    {
        var c = new SqliteConnection(_connStr);
        c.Open();
        using var pragma = c.CreateCommand();
        pragma.CommandText =
            "PRAGMA journal_mode=WAL;" +
            "PRAGMA synchronous=NORMAL;" +
            "PRAGMA foreign_keys=ON;" +
            "PRAGMA busy_timeout=5000;";
        pragma.ExecuteNonQuery();
        return c;
    }

    /// <summary>Runs a read against a pooled connection.</summary>
    public async Task<T> ReadAsync<T>(Func<SqliteConnection, Task<T>> work)
    {
        using var c = Open();
        return await work(c);
    }

    /// <summary>
    /// Runs a write under a process-wide writer lock. Each store write here is
    /// a single statement (atomic in SQLite), so no explicit transaction is
    /// needed; add a transactional overload when multi-statement writes appear.
    /// </summary>
    public async Task WriteAsync(Func<SqliteConnection, Task> work)
    {
        await _writerLock.WaitAsync();
        try
        {
            using var c = Open();
            await work(c);
        }
        finally
        {
            _writerLock.Release();
        }
    }

    private static void Migrate(SqliteConnection c)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = Schema;
        cmd.ExecuteNonQuery();

        EnsurePromptsFts(c);
    }

    /// <summary>
    /// Set up the FTS5 virtual table that mirrors the prompts table. Idempotent.
    /// First-time creation backfills from any rows already in prompts so the
    /// switch from the in-memory filter doesn't lose existing data.
    /// </summary>
    private static void EnsurePromptsFts(SqliteConnection c)
    {
        bool ftsExisted;
        using (var check = c.CreateCommand())
        {
            check.CommandText =
                "SELECT 1 FROM sqlite_master WHERE type='table' AND name='prompts_fts';";
            ftsExisted = check.ExecuteScalar() is not null;
        }

        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = FtsSchema;
            cmd.ExecuteNonQuery();
        }

        if (ftsExisted) return;

        using var backfill = c.CreateCommand();
        backfill.CommandText =
            "INSERT INTO prompts_fts(rowid, title, body, tags) " +
            "SELECT rowid, title, body, tags FROM prompts;";
        backfill.ExecuteNonQuery();
    }

    private static void Seed(SqliteConnection c)
    {
        SeedProjects(c);
        SeedPrompts(c);
    }

    private static void SeedProjects(SqliteConnection c)
    {
        if (Count(c, "projects") > 0) return;

        using var tx = c.BeginTransaction();
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO projects (id, name, description, folder_path, status, last_activity) " +
            "VALUES ($id, $name, $desc, $path, 0, $la);";
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("$name", "ClaudePM");
        cmd.Parameters.AddWithValue("$desc", "This very app — your first registered project.");
        cmd.Parameters.AddWithValue("$path", @"C:\dev\ClaudePM");
        cmd.Parameters.AddWithValue("$la", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    private static void SeedPrompts(SqliteConnection c)
    {
        // Idempotent upsert by title: existing user DBs still get the
        // curated set on next launch (legacy seeds and any user-created
        // prompts are left alone).
        var existingTitles = new HashSet<string>(StringComparer.Ordinal);
        using (var query = c.CreateCommand())
        {
            query.CommandText = "SELECT title FROM prompts;";
            using var r = query.ExecuteReader();
            while (r.Read()) existingTitles.Add(r.GetString(0));
        }

        var toInsert = SeedPromptsData.All
            .Where(p => !existingTitles.Contains(p.Title))
            .ToList();
        if (toInsert.Count == 0) return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var tx = c.BeginTransaction();
        foreach (var s in toInsert)
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                "INSERT INTO prompts (id, title, body, category, tags, usage_count, is_favorite, created, modified) " +
                "VALUES ($id, $title, $body, $cat, $tags, 0, 0, $now, $now);";
            cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("$title", s.Title);
            cmd.Parameters.AddWithValue("$body", s.Body);
            cmd.Parameters.AddWithValue("$cat", s.Category);
            cmd.Parameters.AddWithValue("$tags", TagSerializer.Serialize(s.Tags.ToList()));
            cmd.Parameters.AddWithValue("$now", now);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private static long Count(SqliteConnection c, string table)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM " + table + ";";
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    private const string Schema = @"
        CREATE TABLE IF NOT EXISTS projects (
            id            TEXT PRIMARY KEY,
            name          TEXT NOT NULL,
            description   TEXT NOT NULL DEFAULT '',
            folder_path   TEXT NOT NULL DEFAULT '',
            status        INTEGER NOT NULL DEFAULT 0,
            last_activity INTEGER NOT NULL
        ) STRICT;

        CREATE TABLE IF NOT EXISTS prompts (
            id          TEXT PRIMARY KEY,
            title       TEXT NOT NULL,
            body        TEXT NOT NULL,
            category    TEXT NOT NULL DEFAULT 'General',
            tags        TEXT NOT NULL DEFAULT '[]',
            usage_count INTEGER NOT NULL DEFAULT 0,
            is_favorite INTEGER NOT NULL DEFAULT 0,
            created     INTEGER NOT NULL,
            modified    INTEGER NOT NULL
        ) STRICT;

        CREATE TABLE IF NOT EXISTS notes (
            id         TEXT PRIMARY KEY,
            title      TEXT NOT NULL,
            body       TEXT NOT NULL,
            tags       TEXT NOT NULL DEFAULT '[]',
            project_id TEXT,
            created    INTEGER NOT NULL
        ) STRICT;

        CREATE INDEX IF NOT EXISTS idx_notes_project ON notes(project_id);

        CREATE TABLE IF NOT EXISTS prompt_versions (
            id         TEXT PRIMARY KEY,
            prompt_id  TEXT NOT NULL,
            title      TEXT NOT NULL,
            body       TEXT NOT NULL,
            category   TEXT NOT NULL DEFAULT 'General',
            tags       TEXT NOT NULL DEFAULT '[]',
            captured   INTEGER NOT NULL,
            FOREIGN KEY(prompt_id) REFERENCES prompts(id) ON DELETE CASCADE
        ) STRICT;

        CREATE INDEX IF NOT EXISTS idx_prompt_versions_prompt
            ON prompt_versions(prompt_id, captured DESC);
    ";

    /// <summary>
    /// FTS5 external-content index over prompts.rowid. Triggers keep it in
    /// sync; CREATE statements are IF NOT EXISTS so re-running migration is
    /// safe.
    /// </summary>
    private const string FtsSchema = @"
        CREATE VIRTUAL TABLE IF NOT EXISTS prompts_fts USING fts5(
            title, body, tags,
            content='prompts', content_rowid='rowid'
        );

        CREATE TRIGGER IF NOT EXISTS prompts_ai AFTER INSERT ON prompts BEGIN
          INSERT INTO prompts_fts(rowid, title, body, tags)
          VALUES (new.rowid, new.title, new.body, new.tags);
        END;

        CREATE TRIGGER IF NOT EXISTS prompts_ad AFTER DELETE ON prompts BEGIN
          INSERT INTO prompts_fts(prompts_fts, rowid, title, body, tags)
          VALUES('delete', old.rowid, old.title, old.body, old.tags);
        END;

        CREATE TRIGGER IF NOT EXISTS prompts_au AFTER UPDATE ON prompts BEGIN
          INSERT INTO prompts_fts(prompts_fts, rowid, title, body, tags)
          VALUES('delete', old.rowid, old.title, old.body, old.tags);
          INSERT INTO prompts_fts(rowid, title, body, tags)
          VALUES (new.rowid, new.title, new.body, new.tags);
        END;
    ";

    public void Dispose() => _writerLock.Dispose();
}
