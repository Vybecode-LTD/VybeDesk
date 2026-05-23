using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;

namespace ClaudePM.Services.Ai;

/// <summary>
/// Calls the Anthropic Messages API directly over HTTPS. The API key is read
/// from <see cref="ISecureKeyStore"/> on every call, so a key saved at runtime
/// takes effect without an app restart.
/// </summary>
public sealed class AnthropicChatService : IAiService, IDisposable
{
    private const string Endpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const int MaxTokens = 4096;

    private readonly ISecureKeyStore _keys;
    private readonly ISettingsService _settings;
    private readonly HttpClient _http = new();

    public AnthropicChatService(ISecureKeyStore keys, ISettingsService settings)
    {
        _keys = keys;
        _settings = settings;
    }

    public bool IsConfigured => _keys.HasKey;

    public Task<string> CompleteAsync(
        string systemPrompt, string userPrompt, CancellationToken ct = default)
        => SendAsync(systemPrompt,
            new[] { new Message { Role = "user", Content = userPrompt } }, ct);

    public Task<string> ChatAsync(
        string systemPrompt, IReadOnlyList<ChatMessage> history, CancellationToken ct = default)
        => SendAsync(systemPrompt,
            history.Select(h => new Message { Role = h.Role, Content = h.Text }).ToArray(), ct);

    private async Task<string> SendAsync(string system, Message[] messages, CancellationToken ct)
    {
        var key = _keys.LoadKey();
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "No Anthropic API key is configured. Add one in Settings.");

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("x-api-key", key);
        request.Headers.Add("anthropic-version", AnthropicVersion);
        request.Content = JsonContent.Create(new MessagesRequest
        {
            Model = _settings.Current.Model,
            MaxTokens = MaxTokens,
            System = system,
            Messages = messages,
        });

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                "Anthropic API error (" + (int)response.StatusCode + "): " + body);

        var parsed = JsonSerializer.Deserialize<MessagesResponse>(body);
        var text = parsed?.Content?.FirstOrDefault(b => b.Type == "text")?.Text;
        return text ?? "(empty response)";
    }

    public void Dispose() => _http.Dispose();

    private sealed class MessagesRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; }
        [JsonPropertyName("system")] public string System { get; set; } = "";
        [JsonPropertyName("messages")] public Message[] Messages { get; set; } = Array.Empty<Message>();
    }

    private sealed class Message
    {
        [JsonPropertyName("role")] public string Role { get; set; } = "";
        [JsonPropertyName("content")] public string Content { get; set; } = "";
    }

    private sealed class MessagesResponse
    {
        [JsonPropertyName("content")] public ContentBlock[]? Content { get; set; }
    }

    private sealed class ContentBlock
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("text")] public string? Text { get; set; }
    }
}
