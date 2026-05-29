using VybeDesk.Core.Models;

namespace VybeDesk.Core.Services;

/// <summary>
/// Cross-module coordinator: lets a non-Notebook view model hand a
/// fix-prompt to the Notebook + trigger sidebar navigation to it. Used
/// today by the Documentation tab's "Apply with AI" button on the
/// reconciliation + project-audit fix-prompt panels (M3 roadmap item
/// #11). The Notebook itself owns the actual chat send — this
/// coordinator only PREPARES the Notebook (active project + chat input)
/// and surfaces the page so the user can review and click Send.
///
/// Safety contract: this interface must NEVER provide an unattended
/// auto-execute path. All AI-initiated filesystem writes flow through
/// the Notebook's preview/execute/undo gate with the user in the loop.
///
/// Designed to be a tiny one-method shared surface so the calling
/// modules and the Notebook share nothing of each other's internals,
/// matching the IBugFixedNotifier pattern.
/// </summary>
public interface INotebookOpener
{
    /// <summary>
    /// Populate the Notebook with the given fix-prompt, set its active
    /// project so file actions are scoped correctly, and navigate the
    /// app shell to the Notebook page. Does NOT auto-send — the user
    /// reviews + clicks Send themselves so they can edit the prompt
    /// first if they want.
    /// </summary>
    /// <param name="project">Active project for the Notebook (sets ActiveProject).</param>
    /// <param name="prompt">Fix-prompt text loaded into ChatInput.</param>
    void OpenWithFixPrompt(Project project, string prompt);
}
