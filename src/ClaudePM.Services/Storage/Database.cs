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
        if (Count(c, "prompts") > 0) return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        (string title, string body, string cat, string tags)[] samples =
        {
            ("Scaffold a new module",
             "Scaffold a new {{module_name}} module that follows the existing project " +
             "conventions. Place files in the correct layer and update DI registration.",
             "Claude Code", "[\"scaffold\",\"template\"]"),
            ("Reconcile documentation",
             "Review every doc in this project. List the inconsistencies you find, then " +
             "fix them so the docs agree with each other.",
             "Documentation", "[\"docs\"]"),
        };

        using var tx = c.BeginTransaction();
        foreach (var s in samples)
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                "INSERT INTO prompts (id, title, body, category, tags, usage_count, is_favorite, created, modified) " +
                "VALUES ($id, $title, $body, $cat, $tags, 0, 0, $now, $now);";
            cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("$title", s.title);
            cmd.Parameters.AddWithValue("$body", s.body);
            cmd.Parameters.AddWithValue("$cat", s.cat);
            cmd.Parameters.AddWithValue("$tags", s.tags);
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
    ";

    public void Dispose() => _writerLock.Dispose();
}
