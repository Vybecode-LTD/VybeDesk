using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private const int MaxRetries = 3;
    private static readonly HashSet<int> RetryableStatusCodes = new() { 429, 503, 529 };

    private readonly ISecureKeyStore _keys;
    private readonly ISettingsService _settings;
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public AnthropicChatService(ISecureKeyStore keys, ISettingsService settings)
        : this(keys, settings, new HttpClient(), ownsHttp: true) { }

    /// <summary>Test-friendly overload that takes a pre-configured HttpClient.</summary>
    public AnthropicChatService(ISecureKeyStore keys, ISettingsService settings, HttpClient http)
        : this(keys, settings, http, ownsHttp: false) { }

    private AnthropicChatService(ISecureKeyStore keys, ISettingsService settings,
                                 HttpClient http, bool ownsHttp)
    {
        _keys = keys;
        _settings = settings;
        _http = http;
        _ownsHttp = ownsHttp;
    }

    public bool IsConfigured => _keys.HasKey;

    public Task<string> CompleteAsync(
        string systemPrompt, string userPrompt, CancellationToken ct = default)
        => SendNonStreamingAsync(systemPrompt,
            new[] { new Message { Role = "user", Content = userPrompt } }, ct);

    public Task<string> ChatAsync(
        string systemPrompt, IReadOnlyList<ChatMessage> history, CancellationToken ct = default)
        => SendNonStreamingAsync(systemPrompt,
            history.Select(h => new Message { Role = h.Role, Content = h.Text }).ToArray(), ct);

    public async Task<AgentChatResponse> AgentChatAsync(
        string systemPrompt,
        IReadOnlyList<AgentTurn> history,
        IReadOnlyList<AgentTool> tools,
        Action<string>? onTextDelta = null,
        CancellationToken ct = default)
    {
        var payload = BuildStreamingPayload(systemPrompt, history, tools);
        using var response = await SendWithRetryAsync(
            () =>
            {
                var req = BuildRequest(stream: true);
                req.Content = JsonContent.Create(payload);
                return req;
            },
            HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                "Anthropic API error (" + (int)response.StatusCode + "): " + error);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await ParseSseStreamAsync(stream, onTextDelta, ct);
    }

    private async Task<string> SendNonStreamingAsync(string system, Message[] messages, CancellationToken ct)
    {
        // System is sent as a single-element array so we can attach
        // cache_control. The server treats array-form system identically
        // to string-form; the array form just gives us a place to hang
        // the cache breakpoint.
        var payload = new MessagesRequest
        {
            Model = _settings.Current.Model,
            MaxTokens = MaxTokens,
            System = new[]
            {
                new SystemBlock
                {
                    Text = system,
                    CacheControl = new CacheControl(),
                },
            },
            Messages = messages,
        };

        using var response = await SendWithRetryAsync(
            () =>
            {
                var req = BuildRequest(stream: false);
                req.Content = JsonContent.Create(payload);
                return req;
            },
            HttpCompletionOption.ResponseContentRead, ct);

        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                "Anthropic API error (" + (int)response.StatusCode + "): " + body);

        var parsed = JsonSerializer.Deserialize<MessagesResponse>(body);
        var text = parsed?.Content?.FirstOrDefault(b => b.Type == "text")?.Text;
        return text ?? "(empty response)";
    }

    /// <summary>
    /// Sends the request, retrying up to <see cref="MaxRetries"/> times on
    /// 429 / 503 / 529 responses. Honors the <c>Retry-After</c> header when
    /// the server provides one; otherwise backs off exponentially starting
    /// at 1 s with a small jitter. The request must be rebuildable because
    /// <see cref="HttpRequestMessage"/> can't be re-sent.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        HttpCompletionOption completion,
        CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(1);
        for (int attempt = 0; ; attempt++)
        {
            using var request = requestFactory();
            var response = await _http.SendAsync(request, completion, ct);

            if (!RetryableStatusCodes.Contains((int)response.StatusCode) ||
                attempt == MaxRetries)
                return response;

            var wait = ParseRetryAfter(response) ?? AddJitter(delay);
            response.Dispose();
            await Task.Delay(wait, ct);
            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, TimeSpan.FromMinutes(1).Ticks));
        }
    }

    private static TimeSpan? ParseRetryAfter(HttpResponseMessage response)
    {
        var ra = response.Headers.RetryAfter;
        if (ra is null) return null;
        if (ra.Delta is { } delta) return delta;
        if (ra.Date is { } when)
        {
            var dur = when - DateTimeOffset.UtcNow;
            return dur > TimeSpan.Zero ? dur : TimeSpan.Zero;
        }
        return null;
    }

    private static TimeSpan AddJitter(TimeSpan baseDelay)
        => baseDelay + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));

    private HttpRequestMessage BuildRequest(bool stream)
    {
        var key = _keys.LoadKey();
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "No Anthropic API key is configured. Add one in Settings.");
        foreach (var c in key)
        {
            if (c > 127)
                throw new InvalidOperationException(
                    "Stored Anthropic API key contains non-ASCII characters " +
                    "(often a smart-quote or em-dash from a rich-text copy-paste). " +
                    "Open Settings → Clear Key → re-paste from the Anthropic console.");
        }

        var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        req.Headers.Add("x-api-key", key);
        req.Headers.Add("anthropic-version", AnthropicVersion);
        if (stream)
            req.Headers.Add("accept", "text/event-stream");
        return req;
    }

    /// <summary>
    /// Build the streaming-mode payload as a JsonObject. We accept the
    /// flexibility cost so tool definitions and rich content blocks (string
    /// or array) serialize correctly without bespoke DTOs for every shape.
    /// Adds prompt-caching breakpoints on the system block and the LAST
    /// tool — both are stable across turns of a conversation, so on requests
    /// past the model's minimum cacheable size the server can read them
    /// from cache at ~10% of the base input cost. Caching silently no-ops
    /// when the total prompt is below the model's minimum (4096 tokens
    /// for Opus 4.7).
    /// </summary>
    private JsonObject BuildStreamingPayload(
        string system, IReadOnlyList<AgentTurn> history, IReadOnlyList<AgentTool> tools)
    {
        var payload = new JsonObject
        {
            ["model"] = _settings.Current.Model,
            ["max_tokens"] = MaxTokens,
            ["stream"] = true,
            ["system"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = system,
                    ["cache_control"] = new JsonObject { ["type"] = "ephemeral" },
                },
            },
        };

        var messages = new JsonArray();
        foreach (var turn in history)
            messages.Add(SerializeTurn(turn));
        payload["messages"] = messages;

        if (tools is { Count: > 0 })
        {
            var toolArr = new JsonArray();
            for (int i = 0; i < tools.Count; i++)
            {
                var t = tools[i];
                var toolObj = new JsonObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["input_schema"] = JsonNode.Parse(t.InputSchema.GetRawText()),
                };
                // The breakpoint on the LAST tool caches the entire tools
                // block as a unit — cheaper than per-tool breakpoints and
                // matches Anthropic's hierarchical (tools → system →
                // messages) caching order.
                if (i == tools.Count - 1)
                    toolObj["cache_control"] = new JsonObject { ["type"] = "ephemeral" };
                toolArr.Add(toolObj);
            }
            payload["tools"] = toolArr;
        }

        return payload;
    }

    private static JsonObject SerializeTurn(AgentTurn turn)
    {
        var content = new JsonArray();
        foreach (var block in turn.Content)
        {
            switch (block)
            {
                case AgentTextBlock t:
                    content.Add(new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = t.Text,
                    });
                    break;
                case AgentToolUseBlock u:
                    content.Add(new JsonObject
                    {
                        ["type"] = "tool_use",
                        ["id"] = u.Id,
                        ["name"] = u.Name,
                        ["input"] = JsonNode.Parse(u.Input.GetRawText()),
                    });
                    break;
                case AgentToolResultBlock r:
                    var resObj = new JsonObject
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = r.ToolUseId,
                        ["content"] = r.Content,
                    };
                    if (r.IsError) resObj["is_error"] = true;
                    content.Add(resObj);
                    break;
            }
        }
        return new JsonObject
        {
            ["role"] = turn.Role,
            ["content"] = content,
        };
    }

    /// <summary>
    /// Reads the SSE stream produced by the Messages API and reassembles the
    /// assistant turn. We track the in-flight block by index, accumulate text
    /// deltas verbatim, and accumulate input_json_delta fragments for tool_use
    /// blocks so we can parse the JSON once when the block ends.
    /// </summary>
    private static async Task<AgentChatResponse> ParseSseStreamAsync(
        Stream stream, Action<string>? onTextDelta, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var blocks = new List<AgentContentBlock>();
        var pendingTextByIndex = new Dictionary<int, StringBuilder>();
        var pendingToolByIndex = new Dictionary<int, ToolUseInProgress>();
        var orderByIndex = new SortedDictionary<int, object>();
        var stopReason = "end_turn";

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

            var data = line.Substring(5).Trim();
            if (data.Length == 0) continue;

            JsonElement evt;
            try { evt = JsonSerializer.Deserialize<JsonElement>(data); }
            catch { continue; }

            if (!evt.TryGetProperty("type", out var typeEl)) continue;
            switch (typeEl.GetString())
            {
                case "content_block_start":
                {
                    var index = evt.GetProperty("index").GetInt32();
                    var block = evt.GetProperty("content_block");
                    var blockType = block.GetProperty("type").GetString();
                    if (blockType == "text")
                    {
                        var sb = new StringBuilder();
                        if (block.TryGetProperty("text", out var initial) &&
                            initial.ValueKind == JsonValueKind.String)
                            sb.Append(initial.GetString());
                        pendingTextByIndex[index] = sb;
                        orderByIndex[index] = sb;
                    }
                    else if (blockType == "tool_use")
                    {
                        var inProgress = new ToolUseInProgress(
                            block.GetProperty("id").GetString() ?? "",
                            block.GetProperty("name").GetString() ?? "",
                            new StringBuilder());
                        pendingToolByIndex[index] = inProgress;
                        orderByIndex[index] = inProgress;
                    }
                    break;
                }
                case "content_block_delta":
                {
                    var index = evt.GetProperty("index").GetInt32();
                    var delta = evt.GetProperty("delta");
                    var deltaType = delta.GetProperty("type").GetString();
                    if (deltaType == "text_delta" &&
                        pendingTextByIndex.TryGetValue(index, out var sb))
                    {
                        var chunk = delta.GetProperty("text").GetString() ?? "";
                        sb.Append(chunk);
                        if (chunk.Length > 0) onTextDelta?.Invoke(chunk);
                    }
                    else if (deltaType == "input_json_delta" &&
                             pendingToolByIndex.TryGetValue(index, out var tool))
                    {
                        tool.InputJson.Append(delta.GetProperty("partial_json").GetString() ?? "");
                    }
                    break;
                }
                case "content_block_stop":
                {
                    // Block bodies are now complete; the JSON parse for tool_use
                    // happens when we materialize the response so a single
                    // malformed tool_use surfaces clearly.
                    break;
                }
                case "message_delta":
                {
                    if (evt.TryGetProperty("delta", out var d) &&
                        d.TryGetProperty("stop_reason", out var sr) &&
                        sr.ValueKind == JsonValueKind.String)
                        stopReason = sr.GetString() ?? stopReason;
                    break;
                }
                case "message_stop":
                {
                    // End of stream — outer loop will exit on EOF.
                    break;
                }
                case "error":
                {
                    var msg = evt.TryGetProperty("error", out var e) &&
                              e.TryGetProperty("message", out var m)
                        ? m.GetString()
                        : "Unknown streaming error.";
                    throw new InvalidOperationException("Anthropic stream error: " + msg);
                }
            }
        }

        foreach (var entry in orderByIndex.Values)
        {
            switch (entry)
            {
                case StringBuilder sb:
                    blocks.Add(new AgentTextBlock(sb.ToString()));
                    break;
                case ToolUseInProgress tool:
                {
                    var raw = tool.InputJson.Length == 0 ? "{}" : tool.InputJson.ToString();
                    JsonElement input;
                    try { input = JsonSerializer.Deserialize<JsonElement>(raw); }
                    catch (JsonException)
                    {
                        // Surface as an empty-input tool_use so the caller can
                        // still see the call but won't execute against garbage.
                        input = JsonSerializer.Deserialize<JsonElement>("{}");
                    }
                    blocks.Add(new AgentToolUseBlock(tool.Id, tool.Name, input));
                    break;
                }
            }
        }

        return new AgentChatResponse(stopReason, blocks);
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    private sealed record ToolUseInProgress(string Id, string Name, StringBuilder InputJson);

    private sealed class MessagesRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; }
        [JsonPropertyName("system")] public SystemBlock[] System { get; set; } = Array.Empty<SystemBlock>();
        [JsonPropertyName("messages")] public Message[] Messages { get; set; } = Array.Empty<Message>();
    }

    private sealed class SystemBlock
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "text";
        [JsonPropertyName("text")] public string Text { get; set; } = "";
        [JsonPropertyName("cache_control")] public CacheControl? CacheControl { get; set; }
    }

    private sealed class CacheControl
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "ephemeral";
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
