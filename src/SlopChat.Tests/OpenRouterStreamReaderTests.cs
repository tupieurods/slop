using System.Text;
using SlopChat.Models;
using SlopChat.Services;

namespace SlopChat.Tests
{
  public class OpenRouterStreamReaderTests
  {
    private static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultTotalTimeout = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task ReadAsync_ContentSplitAcrossChunks_AssemblesText()
    {
      const string sse = """
        data: {"id":"gen-1","model":"m","choices":[{"delta":{"role":"assistant","content":"Hel"}}]}

        data:{"id":"gen-1","model":"m","choices":[{"delta":{"content":"lo, "}}]}

        data: {"id":"gen-1","model":"m","choices":[{"delta":{"content":"world"},"finish_reason":"stop","native_finish_reason":"STOP"}]}

        data: [DONE]

        """;

      ChatCompletionResponse response = await ReadAsync(sse);

      ChatChoice choice = Assert.Single(response.Choices);
      Assert.Equal("Hello, world", choice.Message!.Content);
      Assert.Null(choice.Message.Reasoning);
      Assert.Null(choice.Message.ToolCalls);
      Assert.Equal("stop", choice.FinishReason);
      Assert.Equal("STOP", choice.NativeFinishReason);
      Assert.Equal("gen-1", response.Id);
      Assert.Equal("m", response.Model);
    }

    [Fact]
    public async Task ReadAsync_ReasoningDeltas_AssemblesReasoningAndKeepsContentNull()
    {
      const string sse = """
        data: {"choices":[{"delta":{"reasoning":"Think"}}]}

        data: {"choices":[{"delta":{"reasoning":"ing..."}}]}

        data: {"choices":[{"delta":{},"finish_reason":"stop"}]}

        data: [DONE]

        """;

      ChatCompletionResponse response = await ReadAsync(sse);

      ChatChoiceMessage message = response.Choices[0].Message!;
      Assert.Equal("Thinking...", message.Reasoning);
      Assert.Null(message.Content);
    }

    [Fact]
    public async Task ReadAsync_ToolCallArgumentsSplitOverChunks_MergesArguments()
    {
      const string sse = """
        data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_1","type":"function","function":{"name":"web_search","arguments":"{\"q"}}]}}]}

        data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"uery\":"}}]}}]}

        data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"\"cats\"}"}}]},"finish_reason":"tool_calls"}]}

        data: [DONE]

        """;

      ChatCompletionResponse response = await ReadAsync(sse);

      ChatChoice choice = response.Choices[0];
      Assert.Equal("tool_calls", choice.FinishReason);
      ToolCall call = Assert.Single(choice.Message!.ToolCalls!);
      Assert.Equal("call_1", call.Id);
      Assert.Equal("function", call.Type);
      Assert.Equal("web_search", call.Function.Name);
      Assert.Equal("{\"query\":\"cats\"}", call.Function.Arguments);
    }

    [Fact]
    public async Task ReadAsync_ParallelToolCallsWithDifferentIndexes_KeepsBothInOrder()
    {
      const string sse = """
        data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_a","type":"function","function":{"name":"web_search","arguments":"{\"query\":"}}]}}]}

        data: {"choices":[{"delta":{"tool_calls":[{"index":1,"id":"call_b","type":"function","function":{"name":"fetch_url","arguments":"{\"url\":"}}]}}]}

        data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"\"a\"}"}},{"index":1,"function":{"arguments":"\"b\"}"}}]},"finish_reason":"tool_calls"}]}

        data: [DONE]

        """;

      ChatCompletionResponse response = await ReadAsync(sse);

      List<ToolCall> calls = response.Choices[0].Message!.ToolCalls!;
      Assert.Equal(2, calls.Count);
      Assert.Equal("call_a", calls[0].Id);
      Assert.Equal("web_search", calls[0].Function.Name);
      Assert.Equal("{\"query\":\"a\"}", calls[0].Function.Arguments);
      Assert.Equal("call_b", calls[1].Id);
      Assert.Equal("fetch_url", calls[1].Function.Name);
      Assert.Equal("{\"url\":\"b\"}", calls[1].Function.Arguments);
    }

    [Fact]
    public async Task ReadAsync_ParallelToolCallsSharingIndex_SplitsByCallId()
    {
      const string sse = """
        data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_a","type":"function","function":{"name":"web_search","arguments":"{}"}}]}}]}

        data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_b","type":"function","function":{"name":"fetch_url","arguments":"{}"}}]},"finish_reason":"tool_calls"}]}

        data: [DONE]

        """;

      ChatCompletionResponse response = await ReadAsync(sse);

      List<ToolCall> calls = response.Choices[0].Message!.ToolCalls!;
      Assert.Equal(["call_a", "call_b"], calls.Select(c => c.Id).ToArray());
      Assert.Equal(["web_search", "fetch_url"], calls.Select(c => c.Function.Name).ToArray());
    }

    [Fact]
    public async Task ReadAsync_ParallelToolCallsSharingIndexWithoutIds_SplitsByName()
    {
      const string sse = """
        data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"name":"web_search","arguments":"{\"query\":"}}]}}]}

        data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"\"a\"}"}}]}}]}

        data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"name":"fetch_url","arguments":"{\"url\":\"b\"}"}}]},"finish_reason":"tool_calls"}]}

        data: [DONE]

        """;

      ChatCompletionResponse response = await ReadAsync(sse);

      List<ToolCall> calls = response.Choices[0].Message!.ToolCalls!;
      Assert.Equal(2, calls.Count);
      Assert.Equal("web_search", calls[0].Function.Name);
      Assert.Equal("{\"query\":\"a\"}", calls[0].Function.Arguments);
      Assert.Equal("fetch_url", calls[1].Function.Name);
      Assert.Equal("{\"url\":\"b\"}", calls[1].Function.Arguments);
    }

    [Fact]
    public async Task ReadAsync_KeepAliveComments_AreIgnored()
    {
      const string sse = """
        : OPENROUTER PROCESSING

        : OPENROUTER PROCESSING

        data: {"choices":[{"delta":{"content":"ok"},"finish_reason":"stop"}]}

        data: [DONE]

        """;

      ChatCompletionResponse response = await ReadAsync(sse);

      Assert.Equal("ok", response.Choices[0].Message!.Content);
    }

    [Fact]
    public async Task ReadAsync_DataAfterDone_IsIgnored()
    {
      const string sse = """
        data: {"choices":[{"delta":{"content":"first"}}]}

        data: [DONE]

        data: {"choices":[{"delta":{"content":" second"}}]}

        """;

      ChatCompletionResponse response = await ReadAsync(sse);

      Assert.Equal("first", response.Choices[0].Message!.Content);
    }

    [Fact]
    public async Task ReadAsync_UsageInLastChunk_IsPickedUp()
    {
      const string sse = """
        data: {"choices":[{"delta":{"content":"hi"},"finish_reason":"stop"}]}

        data: {"choices":[],"usage":{"cost":0.0123}}

        data: [DONE]

        """;

      ChatCompletionResponse response = await ReadAsync(sse);

      Assert.NotNull(response.Usage);
      Assert.Equal(0.0123, response.Usage.Cost);
      Assert.Equal("hi", response.Choices[0].Message!.Content);
    }

    [Fact]
    public async Task ReadAsync_MidStreamError_ThrowsHttpRequestException()
    {
      const string sse = """
        data: {"choices":[{"delta":{"content":"partial"}}]}

        data: {"error":{"code":502,"message":"Provider disconnected"},"choices":[{"delta":{"content":""},"finish_reason":"error"}]}

        """;

      HttpRequestException ex = await Assert.ThrowsAsync<HttpRequestException>(() => ReadAsync(sse));

      Assert.Equal("OpenRouter stream error 502: Provider disconnected", ex.Message);
    }

    [Fact]
    public async Task ReadAsync_StreamStalls_ThrowsTimeoutException()
    {
      using var stream = new StallingStream(Encoding.UTF8.GetBytes(": OPENROUTER PROCESSING\n\n"));

      await Assert.ThrowsAsync<TimeoutException>(
        () => OpenRouterStreamReader.ReadAsync(stream, TimeSpan.FromMilliseconds(100), DefaultTotalTimeout, CancellationToken.None)
      );
    }

    [Fact]
    public async Task ReadAsync_KeepAlivesBeyondTotalTimeout_ThrowsTimeoutException()
    {
      using var stream = new KeepAliveStream(TimeSpan.FromMilliseconds(10));

      TimeoutException ex = await Assert.ThrowsAsync<TimeoutException>(
        () => OpenRouterStreamReader.ReadAsync(stream, DefaultIdleTimeout, TimeSpan.FromMilliseconds(200), CancellationToken.None)
      );

      Assert.StartsWith("OpenRouter stream exceeded", ex.Message);
    }

    [Fact]
    public async Task ReadAsync_OuterTokenCancelled_ThrowsOperationCanceled()
    {
      using var stream = new StallingStream([]);
      using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

      await Assert.ThrowsAnyAsync<OperationCanceledException>(
        () => OpenRouterStreamReader.ReadAsync(stream, DefaultIdleTimeout, DefaultTotalTimeout, cts.Token)
      );
    }

    private static async Task<ChatCompletionResponse> ReadAsync(string sse)
    {
      using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sse));
      return await OpenRouterStreamReader.ReadAsync(stream, DefaultIdleTimeout, DefaultTotalTimeout, CancellationToken.None);
    }

    private abstract class ReadOnlyTestStream: Stream
    {
      public override bool CanRead => true;

      public override bool CanSeek => false;

      public override bool CanWrite => false;

      public override long Length => throw new NotSupportedException();

      public override long Position
      {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
      }

      public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

      public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

      public override void Flush()
      {
      }

      public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

      public override void SetLength(long value) => throw new NotSupportedException();

      public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class KeepAliveStream: ReadOnlyTestStream
    {
      private readonly TimeSpan _interval;

      private static readonly byte[] KeepAlive = Encoding.UTF8.GetBytes(": OPENROUTER PROCESSING\n\n");

      public KeepAliveStream(TimeSpan interval)
      {
        _interval = interval;
      }

      public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
      {
        await Task.Delay(_interval, cancellationToken);
        int count = Math.Min(buffer.Length, KeepAlive.Length);
        KeepAlive.AsMemory(0, count).CopyTo(buffer);
        return count;
      }
    }

    private sealed class StallingStream: ReadOnlyTestStream
    {
      private readonly byte[] _prefix;
      private int _position;

      public StallingStream(byte[] prefix)
      {
        _prefix = prefix;
      }

      public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
      {
        if(_position < _prefix.Length)
        {
          int count = Math.Min(buffer.Length, _prefix.Length - _position);
          _prefix.AsMemory(_position, count).CopyTo(buffer);
          _position += count;
          return count;
        }

        await Task.Delay(Timeout.Infinite, cancellationToken);
        return 0;
      }
    }
  }
}
