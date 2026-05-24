using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using ClaudePM.Services.Ai;
using NSubstitute;
using Xunit;

namespace ClaudePM.Tests;

/// <summary>
/// Tests AnthropicChatService's streaming + tool_use parsing against canned
/// SSE bytes via a fake HttpMessageHandler. No network involved.
/// </summary>
public sealed class AnthropicChatServiceTests
{
    [Fact]
    public async Task AgentChatAsync_StreamsTextDeltas_AndReturnsFullText()
    {
        var sse =
            Sse("message_start", "{\"type\":\"message_start\",\"message\":{\"id\":\"m1\"}}") +
            Sse("content_block_start", "{\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}") +
            Sse("content_block_delta", "{\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Hello\"}}") +
            Sse("content_block_delta", "{\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\" world\"}}") +
            Sse("content_block_stop", "{\"type\":\"content_block_stop\",\"index\":0}") +
            Sse("message_delta", "{\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"}}") +
            Sse("message_stop", "{\"type\":\"message_stop\"}");

        using var service = CreateService(sse, out var _);

        var chunks = new List<string>();
        var resp = await service.AgentChatAsync(
            "sys",
            new[] { AgentTurn.UserText("hi") },
            Array.Empty<AgentTool>(),
            onTextDelta: c => chunks.Add(c));

        Assert.Equal("end_turn", resp.StopReason);
        Assert.False(resp.WantsToolResults);
        Assert.Equal(new[] { "Hello", " world" }, chunks);
        Assert.Equal("Hello world", resp.TextOutput);
        Assert.Empty(resp.ToolUses);
    }

    [Fact]
    public async Task AgentChatAsync_ReassemblesToolUseFromInputJsonDeltas()
    {
        var sse =
            Sse("message_start", "{\"type\":\"message_start\",\"message\":{\"id\":\"m1\"}}") +
            Sse("content_block_start", "{\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}") +
            Sse("content_block_delta", "{\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"I'll create that.\"}}") +
            Sse("content_block_stop", "{\"type\":\"content_block_stop\",\"index\":0}") +
            Sse("content_block_start", "{\"type\":\"content_block_start\",\"index\":1,\"content_block\":{\"type\":\"tool_use\",\"id\":\"toolu_01\",\"name\":\"create_file\",\"input\":{}}}") +
            Sse("content_block_delta", "{\"type\":\"content_block_delta\",\"index\":1,\"delta\":{\"type\":\"input_json_delta\",\"partial_json\":\"{\\\"path\\\":\\\"/tmp/x.txt\\\"\"}}") +
            Sse("content_block_delta", "{\"type\":\"content_block_delta\",\"index\":1,\"delta\":{\"type\":\"input_json_delta\",\"partial_json\":\",\\\"content\\\":\\\"hi\\\"}\"}}") +
            Sse("content_block_stop", "{\"type\":\"content_block_stop\",\"index\":1}") +
            Sse("message_delta", "{\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"tool_use\"}}") +
            Sse("message_stop", "{\"type\":\"message_stop\"}");

        using var service = CreateService(sse, out var _);

        var resp = await service.AgentChatAsync(
            "sys",
            new[] { AgentTurn.UserText("create /tmp/x.txt with 'hi'") },
            new[] { new AgentTool("create_file", "Create a file", ParseSchema(
                "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"content\":{\"type\":\"string\"}},\"required\":[\"path\",\"content\"]}")) });

        Assert.Equal("tool_use", resp.StopReason);
        Assert.True(resp.WantsToolResults);
        Assert.Equal("I'll create that.", resp.TextOutput);
        var toolUse = Assert.Single(resp.ToolUses);
        Assert.Equal("toolu_01", toolUse.Id);
        Assert.Equal("create_file", toolUse.Name);
        Assert.Equal("/tmp/x.txt", toolUse.Input.GetProperty("path").GetString());
        Assert.Equal("hi", toolUse.Input.GetProperty("content").GetString());
    }

    [Fact]
    public async Task AgentChatAsync_RequestBody_IncludesStreamTrueAndTools()
    {
        var sse =
            Sse("message_start", "{\"type\":\"message_start\",\"message\":{\"id\":\"m1\"}}") +
            Sse("content_block_start", "{\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}") +
            Sse("content_block_delta", "{\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"ok\"}}") +
            Sse("content_block_stop", "{\"type\":\"content_block_stop\",\"index\":0}") +
            Sse("message_delta", "{\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"}}") +
            Sse("message_stop", "{\"type\":\"message_stop\"}");

        using var service = CreateService(sse, out var handler);

        await service.AgentChatAsync(
            "sys",
            new[] { AgentTurn.UserText("hi") },
            new[] { new AgentTool("create_folder", "Create a folder", ParseSchema(
                "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"}},\"required\":[\"path\"]}")) });

        var sentBody = JsonDocument.Parse(handler.LastRequestBody!);
        var root = sentBody.RootElement;
        Assert.True(root.GetProperty("stream").GetBoolean());
        Assert.Equal("sys", root.GetProperty("system").GetString());
        var tools = root.GetProperty("tools");
        Assert.Equal(1, tools.GetArrayLength());
        Assert.Equal("create_folder", tools[0].GetProperty("name").GetString());
        Assert.Equal("object", tools[0].GetProperty("input_schema").GetProperty("type").GetString());
    }

    [Fact]
    public async Task AgentChatAsync_ErrorEvent_ThrowsInvalidOperation()
    {
        var sse = Sse("error", "{\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"Try again.\"}}");
        using var service = CreateService(sse, out var _);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AgentChatAsync("sys", new[] { AgentTurn.UserText("x") }, Array.Empty<AgentTool>()));
        Assert.Contains("Try again", ex.Message);
    }

    [Fact]
    public async Task CompleteAsync_NonStreaming_StillWorks()
    {
        var body = "{\"content\":[{\"type\":\"text\",\"text\":\"hi back\"}]}";
        using var service = CreateNonStreamingService(body, out var _);

        var text = await service.CompleteAsync("sys", "hi");
        Assert.Equal("hi back", text);
    }

    [Fact]
    public async Task CompleteAsync_Retries429ThenSucceeds()
    {
        var handler = new ScriptedHandler(
            (HttpStatusCode.TooManyRequests, "{\"error\":\"rate limit\"}", "0"),
            (HttpStatusCode.OK, "{\"content\":[{\"type\":\"text\",\"text\":\"ok\"}]}", null));
        using var service = BuildService(handler);

        var text = await service.CompleteAsync("sys", "hi");

        Assert.Equal("ok", text);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task CompleteAsync_Retries529ThenSucceeds()
    {
        var handler = new ScriptedHandler(
            ((HttpStatusCode)529, "{\"error\":\"overloaded\"}", "0"),
            (HttpStatusCode.OK, "{\"content\":[{\"type\":\"text\",\"text\":\"ok\"}]}", null));
        using var service = BuildService(handler);

        var text = await service.CompleteAsync("sys", "hi");

        Assert.Equal("ok", text);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task CompleteAsync_GivesUpAfterMaxRetries()
    {
        var handler = new ScriptedHandler(
            (HttpStatusCode.TooManyRequests, "rate limit", "0"),
            (HttpStatusCode.TooManyRequests, "rate limit", "0"),
            (HttpStatusCode.TooManyRequests, "rate limit", "0"),
            (HttpStatusCode.TooManyRequests, "rate limit", "0"));
        using var service = BuildService(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteAsync("sys", "hi"));
        Assert.Equal(4, handler.Calls); // 1 initial + 3 retries
    }

    // ─── helpers ──────────────────────────────────────────────────────

    private static string Sse(string evt, string data)
        => "event: " + evt + "\ndata: " + data + "\n\n";

    private static JsonElement ParseSchema(string json)
        => JsonSerializer.Deserialize<JsonElement>(json);

    private static AnthropicChatService CreateService(string sseBody, out FakeHandler handler)
    {
        handler = new FakeHandler(sseBody, "text/event-stream");
        return BuildService(handler);
    }

    private static AnthropicChatService CreateNonStreamingService(string jsonBody, out FakeHandler handler)
    {
        handler = new FakeHandler(jsonBody, "application/json");
        return BuildService(handler);
    }

    private static AnthropicChatService BuildService(HttpMessageHandler handler)
    {
        var keys = Substitute.For<ISecureKeyStore>();
        keys.LoadKey().Returns("test-key");
        keys.HasKey.Returns(true);

        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings { Model = "claude-test" });

        var http = new HttpClient(handler);
        return new AnthropicChatService(keys, settings, http);
    }

    /// <summary>HttpMessageHandler that records the request body and returns canned content.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly string _mediaType;
        public string? LastRequestBody { get; private set; }

        public FakeHandler(string body, string mediaType)
        {
            _body = body;
            _mediaType = mediaType;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, _mediaType),
            };
        }
    }

    /// <summary>Returns a scripted sequence of responses, one per call. Used for retry tests.</summary>
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body, string? RetryAfter)> _responses;
        public int Calls { get; private set; }

        public ScriptedHandler(params (HttpStatusCode Status, string Body, string? RetryAfter)[] responses)
            => _responses = new Queue<(HttpStatusCode, string, string?)>(responses);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var (status, body, retryAfter) = _responses.Dequeue();
            var msg = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            if (retryAfter is not null) msg.Headers.Add("Retry-After", retryAfter);
            return Task.FromResult(msg);
        }
    }
}
