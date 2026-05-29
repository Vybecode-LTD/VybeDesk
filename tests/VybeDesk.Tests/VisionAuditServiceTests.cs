using System.Text.Json;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using VybeDesk.Services.Vision;
using NSubstitute;
using Xunit;

namespace VybeDesk.Tests;

/// <summary>
/// VisionAuditService tests. Per the spec's hard requirements:
/// <list type="bullet">
/// <item>The audit cannot run against an unapproved vision — approval gate
/// is mandatory.</item>
/// <item>The structural audit produces a verdict for every vision
/// statement, even if the AI drops one in its JSON response.</item>
/// </list>
/// Plus the orchestration behaviours that matter for the smoke test:
/// extract returns parsed statements, the markdown report leads with
/// off-track items, and the deep-dive prompt names flagged statements.
/// </summary>
public sealed class VisionAuditServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly IAiService _ai;
    private readonly IDocReconciliationService _docs;
    private readonly VisionAuditService _svc;

    public VisionAuditServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(),
            "vybedesk-tests-vision-svc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        // A minimal but realistic project shape so the structural audit has
        // something to look at. The docs folder + README give the doc
        // reconciliation a small bundle to read; the .csproj triggers the
        // manifest pickup.
        File.WriteAllText(Path.Combine(_tempRoot, "README.md"),
            "# Demo project\n\nUsers can save data. Works offline.");
        File.WriteAllText(Path.Combine(_tempRoot, "Demo.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"/>");
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src"));
        File.WriteAllText(Path.Combine(_tempRoot, "src", "Storage.cs"),
            "namespace Demo; public class Storage {}");

        _ai = Substitute.For<IAiService>();
        _docs = Substitute.For<IDocReconciliationService>();

        // Stub the doc scan to return the README we just wrote — this is
        // what VisionAuditService.ExtractVisionAsync / AuditAsync depend on.
        var readmePath = Path.Combine(_tempRoot, "README.md");
        _docs.ScanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult<IReadOnlyList<DocFile>>(new[]
             {
                 new DocFile(readmePath, "README.md", "README.md",
                             new FileInfo(readmePath).Length, DateTimeOffset.Now),
             }));

        _svc = new VisionAuditService(_ai, _docs);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public async Task AuditAsync_ThrowsWhenVisionIsNotApproved()
    {
        var vision = new VisionRecord
        {
            ProjectId = Guid.NewGuid(),
            Statements = new[] { new VisionStatement { Text = "Anything." } },
            ApprovedAt = null, // explicitly not approved
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _svc.AuditAsync(vision, _tempRoot, AuditMode.Structural));
        Assert.Contains("approved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuditAsync_ThrowsWhenVisionHasNoStatements()
    {
        var vision = new VisionRecord
        {
            ProjectId = Guid.NewGuid(),
            Statements = Array.Empty<VisionStatement>(),
            ApprovedAt = DateTimeOffset.Now,
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _svc.AuditAsync(vision, _tempRoot, AuditMode.Structural));
    }

    [Fact]
    public async Task ExtractVisionAsync_ReturnsParsedStatements()
    {
        _ai.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(
               "{ \"statements\": [\"Users can save data.\", \"Works offline.\"] }"));

        var statements = await _svc.ExtractVisionAsync(_tempRoot);

        Assert.Equal(2, statements.Count);
        Assert.Equal("Users can save data.", statements[0].Text);
        Assert.Equal("Works offline.", statements[1].Text);
    }

    [Fact]
    public async Task StructuralAudit_ProducesVerdictForEveryStatement()
    {
        var s1 = new VisionStatement { Text = "Users can save data." };
        var s2 = new VisionStatement { Text = "Works offline." };
        var s3 = new VisionStatement { Text = "Has a settings screen." };

        var vision = new VisionRecord
        {
            ProjectId = Guid.NewGuid(),
            Statements = new[] { s1, s2, s3 },
            ApprovedAt = DateTimeOffset.Now,
        };

        // Stub the AI to return verdicts for ONLY two of the three statements.
        // The service must fill in the missing third with an OffTrack
        // "missing verdict" so callers can rely on one-verdict-per-statement.
        var canned = JsonSerializer.Serialize(new
        {
            verdicts = new object[]
            {
                new { statementId = s1.Id.ToString(), rank = "OnTrack",  evidence = "Storage.cs present.", recommendation = "ok" },
                new { statementId = s2.Id.ToString(), rank = "AtRisk",   evidence = "No offline marker.", recommendation = "check" },
                // s3 deliberately omitted
            },
        });
        _ai.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(canned));

        var report = await _svc.AuditAsync(vision, _tempRoot, AuditMode.Structural);

        Assert.Equal(AuditMode.Structural, report.Mode);
        Assert.Equal(3, report.Verdicts.Count);
        Assert.All(vision.Statements, s =>
            Assert.Contains(report.Verdicts, v => v.StatementId == s.Id));
        // The missing one was filled with OffTrack.
        var s3Verdict = report.Verdicts.Single(v => v.StatementId == s3.Id);
        Assert.Equal(AlignmentRank.OffTrack, s3Verdict.Rank);
    }

    [Fact]
    public void BuildReportMarkdown_LeadsWithOffTrack()
    {
        var s1 = new VisionStatement { Text = "Has feature A." };
        var s2 = new VisionStatement { Text = "Has feature B." };
        var s3 = new VisionStatement { Text = "Has feature C." };

        var report = new AuditReport(
            AuditMode.Structural,
            new[]
            {
                new StatementVerdict(s1.Id, s1.Text, AlignmentRank.OnTrack, "yes", "nothing"),
                new StatementVerdict(s2.Id, s2.Text, AlignmentRank.OffTrack, "no", "add it"),
                new StatementVerdict(s3.Id, s3.Text, AlignmentRank.AtRisk, "maybe", "verify"),
            },
            DateTimeOffset.Now);

        var md = _svc.BuildReportMarkdown(report, "Demo");

        // Off-track section heading must appear BEFORE on-track / at-risk
        // sections in the rendered markdown.
        var offIdx = md.IndexOf("## Off track");
        var atIdx = md.IndexOf("## At risk");
        var onIdx = md.IndexOf("## On track");
        Assert.True(offIdx >= 0);
        Assert.True(offIdx < atIdx);
        Assert.True(atIdx < onIdx);
    }

    [Theory]
    [InlineData("../../../etc/passwd")]    // traversal escape
    [InlineData("/etc/passwd")]            // rooted absolute (Unix-style)
    [InlineData("C:\\Windows\\System32\\drivers\\etc\\hosts")] // rooted absolute (Windows)
    [InlineData("")]                       // empty
    [InlineData("   ")]                    // whitespace
    public void TryResolveProjectFile_RejectsPathsOutsideRoot(string relativePath)
    {
        Assert.False(VisionAuditService.TryResolveProjectFile(_tempRoot, relativePath, out _));
    }

    [Theory]
    [InlineData("src/Storage.cs")]
    [InlineData("README.md")]
    public void TryResolveProjectFile_AcceptsValidRelativePaths(string relativePath)
    {
        Assert.True(VisionAuditService.TryResolveProjectFile(_tempRoot, relativePath, out var full));
        Assert.StartsWith(Path.GetFullPath(_tempRoot), full);
    }

    [Fact]
    public void BuildDeepDivePrompt_NamesFlaggedItems()
    {
        var s1 = new VisionStatement { Text = "Stays on track." };
        var s2 = new VisionStatement { Text = "Is missing." };

        var report = new AuditReport(
            AuditMode.Structural,
            new[]
            {
                new StatementVerdict(s1.Id, s1.Text, AlignmentRank.OnTrack, "", ""),
                new StatementVerdict(s2.Id, s2.Text, AlignmentRank.OffTrack, "no code found", "add module"),
            },
            DateTimeOffset.Now);

        var prompt = _svc.BuildDeepDivePrompt(report, "Demo");

        Assert.Contains("Is missing.", prompt);
        Assert.Contains("OffTrack", prompt);
        // The vision should be reproduced in full so the agent knows the
        // measuring stick.
        Assert.Contains("Stays on track.", prompt);
    }
}
