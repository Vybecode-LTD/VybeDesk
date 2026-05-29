using Microsoft.Data.Sqlite;

namespace VybeDesk.Services.Storage;

/// <summary>
/// SQLite connection management and schema initialization for VybeDesk.
/// WAL mode, pooled reader connections, and a single serialized writer — per
/// embedded-SQLite guidance. The database file lives at
/// %LOCALAPPDATA%\VybeDesk\vybedesk.db.
/// </summary>
public sealed class Database : IDisposable
{
    private readonly string _connStr;
    private readonly SemaphoreSlim _writerLock = new(1, 1);

    public Database() : this(Path.Combine(Paths.AppDataDir(), "vybedesk.db")) { }

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
        EnsureAgentActionsNewContentColumn(c);
        EnsureProjectsOverrideColumns(c);
        EnsurePromptsProjectIdColumn(c);
    }

    /// <summary>
    /// v0.31 redo support: adds <c>new_content</c> to <c>agent_actions</c> if
    /// missing. Fresh DBs already have the column from the Schema const;
    /// this migration is for existing user DBs where the table was created
    /// before the column existed. Idempotent — bails out cleanly if the
    /// column is already present. Existing rows end up with NewContent =
    /// NULL, which means they are not redoable (RedoLastAsync bails on null
    /// NewContent for CreateFile / EditFile). That's the correct semantic:
    /// we never captured the data we'd need to faithfully redo those.
    /// </summary>
    private static void EnsureAgentActionsNewContentColumn(SqliteConnection c)
        => EnsureColumn(c, "agent_actions", "new_content", "TEXT");

    /// <summary>
    /// Adds <c>project_id</c> to the <c>prompts</c> table if missing.
    /// Nullable TEXT — NULL means the prompt is global (not project-scoped).
    /// Fresh DBs already have the column from the Schema const; this migration
    /// handles existing user DBs whose prompts table predates the column.
    /// </summary>
    private static void EnsurePromptsProjectIdColumn(SqliteConnection c)
        => EnsureColumn(c, "prompts", "project_id", "TEXT");

    /// <summary>
    /// M4 #16 per-project overrides: adds <c>model</c> and
    /// <c>default_output_path</c> to <c>projects</c> if missing. Fresh DBs
    /// already have the columns from the Schema const; this migration handles
    /// existing user DBs whose projects table predates these columns. Both
    /// columns are nullable TEXT — NULL means "fall back to the global
    /// setting from Settings".
    ///
    /// M5 #17 enhancement: adds <c>logo_path</c> (nullable TEXT) for the
    /// per-project logo shown on the Home dashboard card. NULL means "no
    /// logo — render the module glyph as fallback".
    /// </summary>
    private static void EnsureProjectsOverrideColumns(SqliteConnection c)
    {
        EnsureColumn(c, "projects", "model",               "TEXT");
        EnsureColumn(c, "projects", "default_output_path", "TEXT");
        EnsureColumn(c, "projects", "logo_path",           "TEXT");
    }

    /// <summary>
    /// Shared idempotent <c>ALTER TABLE … ADD COLUMN</c> helper used by the
    /// per-table migration methods above. Inspects <c>pragma_table_info</c>
    /// to skip the ALTER when the column already exists.
    /// </summary>
    private static void EnsureColumn(SqliteConnection c, string table, string column, string sqlType)
    {
        using var check = c.CreateCommand();
        check.CommandText =
            "SELECT 1 FROM pragma_table_info('" + table + "') WHERE name = '" + column + "';";
        if (check.ExecuteScalar() is not null) return;

        using var alter = c.CreateCommand();
        alter.CommandText = "ALTER TABLE " + table + " ADD COLUMN " + column + " " + sqlType + ";";
        alter.ExecuteNonQuery();
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
        cmd.Parameters.AddWithValue("$name", "VybeDesk");
        cmd.Parameters.AddWithValue("$desc", "This very app — your first registered project.");
        cmd.Parameters.AddWithValue("$path", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
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
            id                  TEXT PRIMARY KEY,
            name                TEXT NOT NULL,
            description         TEXT NOT NULL DEFAULT '',
            folder_path         TEXT NOT NULL DEFAULT '',
            status              INTEGER NOT NULL DEFAULT 0,
            last_activity       INTEGER NOT NULL,
            model               TEXT,
            default_output_path TEXT,
            logo_path           TEXT
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
            modified    INTEGER NOT NULL,
            project_id  TEXT
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

        CREATE TABLE IF NOT EXISTS bugs (
            id                 TEXT PRIMARY KEY,
            project_id         TEXT NOT NULL,
            title              TEXT NOT NULL DEFAULT '',
            severity           INTEGER NOT NULL DEFAULT 1,
            status             INTEGER NOT NULL DEFAULT 0,
            steps_to_reproduce TEXT NOT NULL DEFAULT '',
            expected_result    TEXT NOT NULL DEFAULT '',
            actual_result      TEXT NOT NULL DEFAULT '',
            area               TEXT NOT NULL DEFAULT '',
            created            INTEGER NOT NULL
        ) STRICT;

        CREATE INDEX IF NOT EXISTS idx_bugs_project ON bugs(project_id);

        CREATE TABLE IF NOT EXISTS testing_plans (
            id                TEXT PRIMARY KEY,
            project_id        TEXT NOT NULL UNIQUE,
            strategy_summary  TEXT NOT NULL DEFAULT '',
            frameworks_json   TEXT NOT NULL DEFAULT '[]',
            kinds_json        TEXT NOT NULL DEFAULT '[]',
            answers_json      TEXT NOT NULL DEFAULT '{}',
            created           INTEGER NOT NULL,
            modified          INTEGER NOT NULL
        ) STRICT;

        CREATE TABLE IF NOT EXISTS vision_records (
            id                TEXT PRIMARY KEY,
            project_id        TEXT NOT NULL UNIQUE,
            statements_json   TEXT NOT NULL DEFAULT '[]',
            approved_at       INTEGER,
            created           INTEGER NOT NULL,
            modified          INTEGER NOT NULL
        ) STRICT;

        CREATE TABLE IF NOT EXISTS audit_history (
            id              TEXT PRIMARY KEY,
            project_id      TEXT NOT NULL,
            mode            INTEGER NOT NULL,
            off_track_count INTEGER NOT NULL DEFAULT 0,
            at_risk_count   INTEGER NOT NULL DEFAULT 0,
            on_track_count  INTEGER NOT NULL DEFAULT 0,
            report_md       TEXT NOT NULL DEFAULT '',
            deep_dive_md    TEXT NOT NULL DEFAULT '',
            verdicts_json   TEXT NOT NULL DEFAULT '[]',
            generated_at    INTEGER NOT NULL
        ) STRICT;

        CREATE INDEX IF NOT EXISTS idx_audit_history_project
            ON audit_history(project_id, generated_at DESC);

        CREATE TABLE IF NOT EXISTS agent_actions (
            id                TEXT NOT NULL PRIMARY KEY,
            project_id        TEXT NOT NULL,
            kind              INTEGER NOT NULL,
            path              TEXT NOT NULL,
            destination_path  TEXT NOT NULL DEFAULT '',
            original_content  TEXT,
            status            INTEGER NOT NULL,
            executed_at       TEXT NOT NULL,
            description       TEXT NOT NULL,
            new_content       TEXT
        ) STRICT;

        CREATE INDEX IF NOT EXISTS idx_agent_actions_project
            ON agent_actions(project_id, executed_at DESC);

        CREATE TABLE IF NOT EXISTS ai_calls (
            id                    TEXT NOT NULL PRIMARY KEY,
            project_id            TEXT,
            module                TEXT NOT NULL DEFAULT '',
            model                 TEXT NOT NULL DEFAULT '',
            input_tokens          INTEGER NOT NULL DEFAULT 0,
            output_tokens         INTEGER NOT NULL DEFAULT 0,
            cache_creation_tokens INTEGER NOT NULL DEFAULT 0,
            cache_read_tokens     INTEGER NOT NULL DEFAULT 0,
            cost_estimate         REAL NOT NULL DEFAULT 0.0,
            duration_ms           INTEGER NOT NULL DEFAULT 0,
            timestamp             INTEGER NOT NULL
        ) STRICT;

        CREATE INDEX IF NOT EXISTS idx_ai_calls_project
            ON ai_calls(project_id, timestamp DESC);

        CREATE INDEX IF NOT EXISTS idx_ai_calls_timestamp
            ON ai_calls(timestamp DESC);
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
