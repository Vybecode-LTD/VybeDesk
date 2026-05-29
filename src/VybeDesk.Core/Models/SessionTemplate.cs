namespace VybeDesk.Core.Models;

/// <summary>
/// Stack template for a Session Builder handoff package. Drives the
/// contents of the generated CLAUDE.md / README / .gitignore / kickoff
/// prompt. Choose <see cref="PlainMonorepo"/> for "no template, give me
/// generic scaffolding" — the safe default.
/// </summary>
public enum SessionTemplate
{
    PlainMonorepo = 0,
    AvaloniaDotNet,
    FastApiPython,
    NextJsTypeScript,
    PythonCli,
}
