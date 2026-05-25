using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using ClaudePM.Services.ProjectHealth;
using NSubstitute;
using Xunit;

namespace ClaudePM.Tests;

/// <summary>
/// Tests for the M5 #17 ProjectHealthService — the per-project metric
/// rollup powering the Home dashboard cards. Uses NSubstitute for the
/// IDocReconciliationService and IAgentActionLogStore dependencies so the
/// tests stay focused on the rollup behaviour itself. Git-binary tests
/// are deliberately skipped (CI variance — git may or may not be on PATH);
/// instead we assert the null-commit-count path for non-repo folders,
/// which is the same code path GetCommitCountSinceAsync takes on a git
/// failure anyway.
/// </summary>
public sealed class ProjectHealthServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly IDocReconciliationService _docs;
    private readonly IAgentActionLogStore _agentLog;
    private readonly ProjectHealthService _service;

    public ProjectHealthServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(),
            "claudepm-tests-health-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        _docs = Substitute.For<IDocReconciliationService>();
        _agentLog = Substitute.For<IAgentActionLogStore>();
        _service = new ProjectHealthService(_docs, _agentLog);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private Project MakeProject(string? folderPath = null, DateTimeOffset? lastActivity = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = "test-project",
            Description = "for tests",
            FolderPath = folderPath ?? _tempRoot,
            LastActivity = lastActivity ?? DateTimeOffset.Now,
        };

    [Fact]
    public async Task ComputeAsync_ReturnsZeroStaleDocs_WhenFolderHasNoMdFiles()
    {
        var project = MakeProject();
        _docs.ScanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DocFile>>(Array.Empty<DocFile>()));
        _docs.AnalyzeStructuralAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<DocFile>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Finding>>(Array.Empty<Finding>()));
        _agentLog.GetByProjectAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AgentActionLogEntry>>(
                Array.Empty<AgentActionLogEntry>()));

        var metrics = await _service.ComputeAsync(project);

        Assert.Equal(0, metrics.StaleDocCount);
    }

    [Fact]
    public async Task ComputeAsync_CountsStructuralFindings_AsStaleDocCount()
    {
        var project = MakeProject();
        File.WriteAllText(Path.Combine(_tempRoot, "README.md"), "# stale\n");

        var findings = new List<Finding>
        {
            new(FindingSeverity.Critical, "stale", "msg1", "README.md"),
            new(FindingSeverity.Warning,  "stale", "msg2", "README.md"),
            new(FindingSeverity.Info,     "todo",  "msg3", "README.md"),
        };
        _docs.ScanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DocFile>>(Array.Empty<DocFile>()));
        _docs.AnalyzeStructuralAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<DocFile>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Finding>>(findings));
        _agentLog.GetByProjectAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AgentActionLogEntry>>(
                Array.Empty<AgentActionLogEntry>()));

        var metrics = await _service.ComputeAsync(project);

        Assert.Equal(3, metrics.StaleDocCount);
    }

    [Fact]
    public async Task ComputeAsync_ReturnsPendingActionCount_FromAgentLog()
    {
        // Spec says: pending = entries where Status == Done. Verify the
        // service filters out Undone entries even though both are stored
        // in the same log.
        var project = MakeProject();
        _docs.ScanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DocFile>>(Array.Empty<DocFile>()));
        _docs.AnalyzeStructuralAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<DocFile>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Finding>>(Array.Empty<Finding>()));

        var entries = new List<AgentActionLogEntry>
        {
            MakeEntry(project.Id, AgentActionLogStatus.Done),
            MakeEntry(project.Id, AgentActionLogStatus.Done),
            MakeEntry(project.Id, AgentActionLogStatus.Done),
            MakeEntry(project.Id, AgentActionLogStatus.Undone),
            MakeEntry(project.Id, AgentActionLogStatus.Undone),
        };
        _agentLog.GetByProjectAsync(project.Id, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AgentActionLogEntry>>(entries));

        var metrics = await _service.ComputeAsync(project);

        Assert.Equal(3, metrics.PendingActionCount);
    }

    [Fact]
    public async Task ComputeAsync_HandlesNonRepoFolder_WithNullCommitCount()
    {
        // _tempRoot has no .git — GetCommitCountSinceAsync returns null in
        // that case. Stubs for the other dependencies are happy-path so
        // any non-null result on commits would point straight at this code
        // path failing its early-return.
        var project = MakeProject();
        _docs.ScanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DocFile>>(Array.Empty<DocFile>()));
        _docs.AnalyzeStructuralAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<DocFile>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Finding>>(Array.Empty<Finding>()));
        _agentLog.GetByProjectAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AgentActionLogEntry>>(
                Array.Empty<AgentActionLogEntry>()));

        var metrics = await _service.ComputeAsync(project);

        Assert.Null(metrics.RecentCommitCount);
    }

    [Fact]
    public async Task ComputeAsync_PassesLastActivity_FromProject()
    {
        var stamp = new DateTimeOffset(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);
        var project = MakeProject(lastActivity: stamp);
        _docs.ScanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DocFile>>(Array.Empty<DocFile>()));
        _docs.AnalyzeStructuralAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<DocFile>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Finding>>(Array.Empty<Finding>()));
        _agentLog.GetByProjectAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AgentActionLogEntry>>(
                Array.Empty<AgentActionLogEntry>()));

        var metrics = await _service.ComputeAsync(project);

        Assert.Equal(stamp, metrics.LastActivity);
    }

    [Fact]
    public async Task ComputeAsync_TolerantToMissingFolder_ReturnsZerosNotThrow()
    {
        // A project whose FolderPath has been deleted (or was never valid)
        // must NOT make ComputeAsync throw — Home cards have to render even
        // for misconfigured projects so the user can fix the path.
        var project = MakeProject(folderPath: Path.Combine(_tempRoot, "does-not-exist"));
        _agentLog.GetByProjectAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AgentActionLogEntry>>(
                Array.Empty<AgentActionLogEntry>()));

        var metrics = await _service.ComputeAsync(project);

        Assert.Equal(0, metrics.StaleDocCount);
        Assert.Null(metrics.RecentCommitCount);
        Assert.Equal(0, metrics.PendingActionCount);
    }

    private static AgentActionLogEntry MakeEntry(Guid projectId, AgentActionLogStatus status)
        => new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Kind = AgentActionKind.CreateFile,
            Path = "C:/scratch/x.txt",
            DestinationPath = "",
            Status = status,
            ExecutedAt = DateTimeOffset.Now,
            Description = "x",
        };
}
