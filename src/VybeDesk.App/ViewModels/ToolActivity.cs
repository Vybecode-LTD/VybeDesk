namespace VybeDesk.App.ViewModels;

/// <summary>
/// A small status entry for a Notebook chat row: the prose summary of a
/// tool invocation that ran (or was proposed) inside the assistant turn.
/// Rendered as italic chips beneath the bubble's prose — green for success,
/// red for failure / blocked.
/// </summary>
public sealed record ToolActivity(string Description, bool Success);
