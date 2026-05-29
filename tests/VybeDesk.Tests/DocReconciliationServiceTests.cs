using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using VybeDesk.Services.Docs;
using NSubstitute;
using Xunit;

namespace VybeDesk.Tests;

/// <summary>
/// Golden-input tests for the audit JSON parser. Claude's response often
/// arrives wrapped in markdown fences or prefixed/suffixed with prose; the
/// brace-scanning extractor + tolerant deserializer have to survive all
/// of these shapes without dropping the result on the floor.
/// </summary>
public sealed class DocReconciliationServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DocFile _doc;

    public DocReconciliationServiceTests()
    {
        _tempDir = Directory.CreateTempSubdirectory().FullName;
        var path = Path.Combine(_tempDir, "CLAUDE.md");
        File.WriteAllText(path, "# Sample project\nSome doc body.\n");
        var info = new FileInfo(path);
        _doc = new DocFile(info.FullName, "CLAUDE.md", "CLAUDE.md", info.Length, info.LastWriteTime);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task AuditAsync_ParsesCleanJsonResponse()
    {
        const string canned =
            "{\"design\":\"A simple project.\"," +
            "\"roadmapItems\":[" +
              "{\"title\":\"Feature A\",\"status\":\"complete\",\"category\":\"feature\",\"source\":\"ROADMAP.md\",\"evidence\":\"checked\"}," +
              "{\"title\":\"Feature B\",\"status\":\"incomplete\",\"category\":\"feature\",\"source\":\"ROADMAP.md\",\"evidence\":\"unchecked\"}]," +
            "\"inconsistencies\":[" +
              "{\"severity\":\"warning\",\"docs\":[\"A.md\",\"B.md\"],\"issue\":\"version mismatch\"}]}";

        var report = await Audit(canned);

        Assert.Equal("A simple project.", report.Design);
        Assert.Equal(2, report.RoadmapItems.Count);
        Assert.Single(report.Complete);
        Assert.Single(report.Incomplete);
        Assert.Equal("Feature A", report.Complete[0].Title);
        var inc = Assert.Single(report.Inconsistencies);
        Assert.Equal(FindingSeverity.Warning, inc.Severity);
        Assert.Equal("version mismatch", inc.Issue);
        Assert.Equal(new[] { "A.md", "B.md" }, inc.Docs);
    }

    [Fact]
    public async Task AuditAsync_StripsMarkdownCodeFence()
    {
        const string canned =
            "```json\n" +
            "{\"design\":\"x\",\"roadmapItems\":[],\"inconsistencies\":[]}\n" +
            "```";

        var report = await Audit(canned);

        Assert.Equal("x", report.Design);
        Assert.Empty(report.RoadmapItems);
        Assert.Empty(report.Inconsistencies);
    }

    [Fact]
    public async Task AuditAsync_SkipsLeadingProse()
    {
        const string canned =
            "Sure! Here's the audit you asked for:\n\n" +
            "{\"design\":\"y\",\"roadmapItems\":[],\"inconsistencies\":[]}";

        var report = await Audit(canned);

        Assert.Equal("y", report.Design);
    }

    [Fact]
    public async Task AuditAsync_IgnoresTrailingProse()
    {
        const string canned =
            "{\"design\":\"z\",\"roadmapItems\":[],\"inconsistencies\":[]}\n\n" +
            "Hope this helps! Let me know if you want more detail.";

        var report = await Audit(canned);

        Assert.Equal("z", report.Design);
    }

    [Fact]
    public async Task AuditAsync_TolerantToCaseAndTrailingCommas()
    {
        const string canned =
            "{\"Design\":\"case insensitive\"," +
            "\"RoadmapItems\":[" +
              "{\"Title\":\"X\",\"Status\":\"DONE\",\"Category\":\"feature\",\"Source\":\"R.md\",\"Evidence\":\"\"}," +
            "]," +
            "\"Inconsistencies\":[]}";

        var report = await Audit(canned);

        Assert.Equal("case insensitive", report.Design);
        var item = Assert.Single(report.RoadmapItems);
        Assert.Equal(AuditItemStatus.Complete, item.Status); // "DONE" maps to Complete
    }

    [Fact]
    public async Task AuditAsync_MalformedJsonThrowsWithRawContent()
    {
        // The service throws (rather than silently returning Empty) when the AI
        // returns non-empty content that can't be parsed — so the caller can
        // surface the raw AI response to the user instead of showing nothing.
        const string canned = "{\"design\":\"oops\",\"roadmapItems\":[{not valid json";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Audit(canned));
        Assert.Contains("Audit response could not be parsed", ex.Message);
    }

    [Fact]
    public async Task AuditAsync_ConversationalResponseThrows()
    {
        // Same as above: conversational prose is non-empty but not parseable JSON —
        // the caller gets the exception and can show the raw text to the user.
        const string canned = "Sorry, I can't audit this right now.";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Audit(canned));
        Assert.Contains("Audit response could not be parsed", ex.Message);
    }

    [Fact]
    public async Task AuditAsync_DropsItemsWithBlankTitle()
    {
        const string canned =
            "{\"design\":\"\",\"roadmapItems\":[" +
              "{\"title\":\"Real item\",\"status\":\"complete\",\"category\":\"\",\"source\":\"\",\"evidence\":\"\"}," +
              "{\"title\":\"\",\"status\":\"incomplete\",\"category\":\"\",\"source\":\"\",\"evidence\":\"\"}," +
              "{\"status\":\"complete\"}" +
            "],\"inconsistencies\":[]}";

        var report = await Audit(canned);

        var item = Assert.Single(report.RoadmapItems);
        Assert.Equal("Real item", item.Title);
    }

    [Fact]
    public async Task AuditAsync_SortsInconsistenciesBySeverity()
    {
        const string canned =
            "{\"design\":\"\",\"roadmapItems\":[],\"inconsistencies\":[" +
              "{\"severity\":\"info\",\"docs\":[\"a\"],\"issue\":\"low\"}," +
              "{\"severity\":\"critical\",\"docs\":[\"b\"],\"issue\":\"high\"}," +
              "{\"severity\":\"warning\",\"docs\":[\"c\"],\"issue\":\"medium\"}]}";

        var report = await Audit(canned);

        Assert.Equal(3, report.Inconsistencies.Count);
        Assert.Equal(FindingSeverity.Critical, report.Inconsistencies[0].Severity);
        Assert.Equal(FindingSeverity.Warning, report.Inconsistencies[1].Severity);
        Assert.Equal(FindingSeverity.Info, report.Inconsistencies[2].Severity);
    }

    private async Task<ProjectAuditReport> Audit(string cannedResponse)
    {
        var ai = Substitute.For<IAiService>();
        ai.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
          .Returns(Task.FromResult(cannedResponse));
        var svc = new DocReconciliationService(ai);
        return await svc.AuditAsync(new[] { _doc });
    }
}
