namespace VybeDesk.Core.Models;

/// <summary>One turn in an AI Notebook conversation.</summary>
public sealed record ChatMessage(string Role, string Text)
{
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";

    public bool IsAssistant => Role == AssistantRole;
}
