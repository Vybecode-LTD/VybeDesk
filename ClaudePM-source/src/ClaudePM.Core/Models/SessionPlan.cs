namespace ClaudePM.Core.Models;

/// <summary>One claude.ai conversation transcript collected for a handoff.</summary>
public sealed class TranscriptEntry
{
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";

    public string DisplayTitle =>
        string.IsNullOrWhiteSpace(Title) ? "(untitled transcript)" : Title;
}

/// <summary>Everything the Session Builder wizard collects (Module 3).</summary>
public sealed class SessionPlan
{
    public string ProjectName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Stack { get; set; } = "";
    public string OutputLocation { get; set; } = "";
    public List<TranscriptEntry> Transcripts { get; set; } = new();
    public List<string> FilePaths { get; set; } = new();

    /// <summary>
    /// Stack template that drives the generated CLAUDE.md / README /
    /// .gitignore / KICKOFF.md scaffolding. Defaults to
    /// <see cref="SessionTemplate.PlainMonorepo"/> — generic content
    /// that works for any project shape.
    /// </summary>
    public SessionTemplate Template { get; set; } = SessionTemplate.PlainMonorepo;
}
