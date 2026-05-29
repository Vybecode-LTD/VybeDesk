using System.Text.Json;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using Microsoft.Data.Sqlite;

namespace VybeDesk.Services.Storage;

/// <summary>
/// SQLite-backed implementation of <see cref="ITestingPlanStore"/>. One row
/// per project (enforced by the UNIQUE constraint on <c>project_id</c>),
/// so <see cref="SaveAsync"/> is an upsert. Lists (frameworks, kinds) and
/// the answers record are stored as JSON TEXT — consistent with how prompt
/// tags are stored elsewhere.
/// </summary>
public sealed class SqliteTestingPlanStore : ITestingPlanStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly Database _db;

    public SqliteTestingPlanStore(Database db) => _db = db;

    public event Action? Changed;

    public Task<TestingPlan?> GetByProjectAsync(Guid projectId, CancellationToken ct = default)
        => _db.ReadAsync<TestingPlan?>(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText =
                "SELECT id, project_id, strategy_summary, frameworks_json, kinds_json, " +
                "answers_json, created, modified " +
                "FROM testing_plans WHERE project_id=$pid LIMIT 1;";
            cmd.Parameters.AddWithValue("$pid", projectId.ToString());

            using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;

            return new TestingPlan
            {
                Id = Guid.Parse(r.GetString(0)),
                ProjectId = Guid.Parse(r.GetString(1)),
                StrategySummary = r.GetString(2),
                Frameworks = JsonSerializer.Deserialize<List<string>>(r.GetString(3), JsonOptions)
                             ?? new List<string>(),
                Kinds = JsonSerializer.Deserialize<List<TestKind>>(r.GetString(4), JsonOptions)
                        ?? new List<TestKind>(),
                Answers = JsonSerializer.Deserialize<QuestionnaireAnswers>(r.GetString(5), JsonOptions)
                          ?? new QuestionnaireAnswers(),
                Created = DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(6)),
                Modified = DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(7)),
            };
        });

    public async Task SaveAsync(TestingPlan plan, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            // Upsert keyed on project_id (the UNIQUE column). Keeps a stable
            // TestingPlan.Id across re-runs of the questionnaire for the
            // same project, while still allowing the caller to mint a new
            // plan if they really mean to (then they'd update project_id).
            using var cmd = c.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO testing_plans
                    (id, project_id, strategy_summary, frameworks_json, kinds_json,
                     answers_json, created, modified)
                VALUES ($id, $pid, $sum, $frm, $kinds, $ans, $cr, $mod)
                ON CONFLICT(project_id) DO UPDATE SET
                    strategy_summary = excluded.strategy_summary,
                    frameworks_json  = excluded.frameworks_json,
                    kinds_json       = excluded.kinds_json,
                    answers_json     = excluded.answers_json,
                    modified         = excluded.modified;";
            Bind(cmd, plan);
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }

    public async Task RemoveAsync(Guid projectId, CancellationToken ct = default)
    {
        await _db.WriteAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM testing_plans WHERE project_id=$pid;";
            cmd.Parameters.AddWithValue("$pid", projectId.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Changed?.Invoke();
    }

    private static void Bind(SqliteCommand cmd, TestingPlan p)
    {
        cmd.Parameters.AddWithValue("$id", p.Id.ToString());
        cmd.Parameters.AddWithValue("$pid", p.ProjectId.ToString());
        cmd.Parameters.AddWithValue("$sum", p.StrategySummary);
        cmd.Parameters.AddWithValue("$frm",
            JsonSerializer.Serialize(p.Frameworks, JsonOptions));
        cmd.Parameters.AddWithValue("$kinds",
            JsonSerializer.Serialize(p.Kinds, JsonOptions));
        cmd.Parameters.AddWithValue("$ans",
            JsonSerializer.Serialize(p.Answers, JsonOptions));
        cmd.Parameters.AddWithValue("$cr", p.Created.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$mod", p.Modified.ToUnixTimeSeconds());
    }
}
