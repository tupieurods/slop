using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SlopChat.Models;
using SlopChat.Services;

namespace SlopChat.Tests
{
  public class OpenRouterClientStreamingTests
  {
    private const string Model = "google/gemini-3.1-pro-preview";

    [Fact]
    public async Task GetCompletionAsync_SendsStreamTrueInRequestBody()
    {
      string? capturedBody = null;
      var client = CreateClient(request =>
      {
        capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        return SseResponse("""
          data: {"choices":[{"delta":{"content":"hi"},"finish_reason":"stop"}]}

          data: [DONE]

          """);
      });

      await client.GetCompletionAsync([ChatMessage.User("hello")], Model, CancellationToken.None);

      Assert.NotNull(capturedBody);
      using JsonDocument body = JsonDocument.Parse(capturedBody);
      Assert.True(body.RootElement.GetProperty("stream").GetBoolean());
    }

    [Fact]
    public async Task GetCompletionAsync_SseResponse_ReturnsAssembledText()
    {
      var client = CreateClient(_ => SseResponse("""
        : OPENROUTER PROCESSING

        data: {"id":"gen-1","model":"m","choices":[{"delta":{"content":"Hello"}}]}

        data: {"id":"gen-1","model":"m","choices":[{"delta":{"content":" there"},"finish_reason":"stop"}]}

        data: [DONE]

        """));

      string text = await client.GetCompletionAsync([ChatMessage.User("hello")], Model, CancellationToken.None);

      Assert.Equal("Hello there", text);
    }

    [Fact]
    public async Task GetCompletionAsync_NonSuccessStatus_ReturnsApiErrorText()
    {
      var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
      {
        Content = new StringContent("""{"error":{"message":"rate limited"}}""", Encoding.UTF8, "application/json")
      });

      string text = await client.GetCompletionAsync([ChatMessage.User("hello")], Model, CancellationToken.None);

      Assert.StartsWith("OpenRouter API error:", text);
      Assert.Contains("429", text);
    }

    [Fact]
    public async Task GetCompletionWithMediaAsync_ToolCallThenText_RunsToolLoopOverStreaming()
    {
      List<string> requestBodies = [];
      string[] responses =
      [
        """
        data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_1","type":"function","function":{"name":"web_search","arguments":"{\"query\":"}}]}}]}

        data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"\"cats\"}"}}]},"finish_reason":"tool_calls"}]}

        data: [DONE]

        """,
        """
        data: {"choices":[{"delta":{"content":"Cats are great"},"finish_reason":"stop"}]}

        data: [DONE]

        """
      ];
      var client = CreateClient(request =>
      {
        requestBodies.Add(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
        return SseResponse(responses[requestBodies.Count - 1]);
      });
      byte[] toolImage = [9, 8, 7];
      var toolExecutor = new StubToolExecutor(new ToolExecutionResult("search results", [toolImage]));

      CompletionResult result = await client.GetCompletionWithMediaAsync([ChatMessage.User("cats?")], Model, CancellationToken.None, toolExecutor);

      Assert.Equal("🌐 Cats are great", result.Text);
      Assert.Equal(toolImage, Assert.Single(result.Images));
      Assert.Equal(("web_search", "{\"query\":\"cats\"}"), Assert.Single(toolExecutor.Calls));
      Assert.Equal(2, requestBodies.Count);
      using JsonDocument secondBody = JsonDocument.Parse(requestBodies[1]);
      JsonElement toolMessage = secondBody.RootElement.GetProperty("messages")
        .EnumerateArray()
        .Single(m => m.GetProperty("role").GetString() == "tool");
      Assert.Equal("call_1", toolMessage.GetProperty("tool_call_id").GetString());
      Assert.Equal("search results", toolMessage.GetProperty("content").GetString());
    }

    private static OpenRouterClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
      => new(new HttpClient(new FuncHttpMessageHandler(handler)), "key", NullLogger<OpenRouterClient>.Instance);

    private static HttpResponseMessage SseResponse(string sse) => new(HttpStatusCode.OK)
    {
      Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
    };

    private sealed class StubToolExecutor: IToolExecutor
    {
      private readonly ToolExecutionResult _result;

      public StubToolExecutor(ToolExecutionResult result)
      {
        _result = result;
      }

      public List<(string Name, string Arguments)> Calls { get; } = [];

      public Task<IReadOnlyList<ToolDefinition>> GetToolDefinitionsAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ToolDefinition>>([new ToolDefinition { Function = new FunctionDefinition { Name = "web_search" } }]);

      public Task<ToolExecutionResult> ExecuteAsync(string toolName, string arguments, CancellationToken ct)
      {
        Calls.Add((toolName, arguments));
        return Task.FromResult(_result);
      }
    }
  }
}
