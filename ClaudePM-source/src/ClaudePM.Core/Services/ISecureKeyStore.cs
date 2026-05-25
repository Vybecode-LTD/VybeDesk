namespace ClaudePM.Core.Services;

/// <summary>Stores the Anthropic API key encrypted at rest in OS-native storage.</summary>
public interface ISecureKeyStore
{
    bool HasKey { get; }
    void SaveKey(string apiKey);
    string? LoadKey();
    void ClearKey();
}
