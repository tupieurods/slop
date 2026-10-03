using System.Text;
using System.Text.Json;
using SlopChat.Models;

namespace SlopChat.Services
{
  internal static class OpenRouterStreamReader
  {
    private const string DataPrefix = "data:";
    private const string DoneMarker = "[DONE]";

    public static async Task<ChatCompletionResponse> ReadAsync(
      Stream body,
      TimeSpan idleTimeout,
      TimeSpan totalTimeout,
      CancellationToken ct
    )
    {
      using var reader = new StreamReader(body, Encoding.UTF8);
      using var totalCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
      totalCts.CancelAfter(totalTimeout);
      using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(totalCts.Token);
      var accumulator = new StreamAccumulator();

      while(true)
      {
        if(totalCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
          throw CreateTotalTimeoutException(totalTimeout, null);
        }

        idleCts.CancelAfter(idleTimeout);
        string? line;
        try
        {
          line = await reader.ReadLineAsync(idleCts.Token);
        }
        catch(Exception ex) when(ex is OperationCanceledException or IOException && idleCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
          if(totalCts.IsCancellationRequested)
          {
            throw CreateTotalTimeoutException(totalTimeout, ex);
          }

          throw new TimeoutException($"OpenRouter stream idle for {idleTimeout.TotalSeconds:0.###}s", ex);
        }

        if(line is null)
        {
          break;
        }

        if(line.Length == 0 || line[0] == ':' || !line.StartsWith(DataPrefix, StringComparison.Ordinal))
        {
          continue;
        }

        string payload = line[DataPrefix.Length..].Trim();
        if(payload == DoneMarker)
        {
          break;
        }

        if(payload.Length == 0)
        {
          continue;
        }

        ChatCompletionChunk? chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(payload, OpenRouterClient.JsonOptions);
        if(chunk is null)
        {
          continue;
        }

        if(chunk.Error is not null)
        {
          throw new HttpRequestException($"OpenRouter stream error {chunk.Error.Code?.ToString()}: {chunk.Error.Message}");
        }

        accumulator.Add(chunk);
      }

      return accumulator.Build();
    }

    private static TimeoutException CreateTotalTimeoutException(TimeSpan totalTimeout, Exception? inner)
      => new($"OpenRouter stream exceeded {totalTimeout.TotalMinutes:0.##} min", inner);

    private sealed class StreamAccumulator
    {
      private readonly StringBuilder _content = new();
      private readonly StringBuilder _reasoning = new();
      private readonly List<ToolCallBuilder> _toolCalls = [];
      private readonly Dictionary<int, ToolCallBuilder> _toolCallsByIndex = [];
      private string? _id;
      private string? _model;
      private string? _finishReason;
      private string? _nativeFinishReason;
      private CompletionUsage? _usage;

      public void Add(ChatCompletionChunk chunk)
      {
        _id ??= chunk.Id;
        _model = chunk.Model ?? _model;
        _usage = chunk.Usage ?? _usage;

        if(chunk.Choices is null)
        {
          return;
        }

        foreach(ChatCompletionChunkChoice choice in chunk.Choices)
        {
          _finishReason = choice.FinishReason ?? _finishReason;
          _nativeFinishReason = choice.NativeFinishReason ?? _nativeFinishReason;

          ChatCompletionChunkDelta? delta = choice.Delta;
          if(delta is null)
          {
            continue;
          }

          _content.Append(delta.Content);
          _reasoning.Append(delta.Reasoning);

          if(delta.ToolCalls is null)
          {
            continue;
          }

          foreach(ToolCallDelta toolCallDelta in delta.ToolCalls)
          {
            AddToolCall(toolCallDelta);
          }
        }
      }

      public ChatCompletionResponse Build()
      {
        List<ToolCall>? toolCalls = _toolCalls.Count > 0
          ? [.._toolCalls.Select(b => b.Build())]
          : null;

        var message = new ChatChoiceMessage
        {
          Role = "assistant",
          Content = _content.Length > 0 ? _content.ToString() : null,
          Reasoning = _reasoning.Length > 0 ? _reasoning.ToString() : null,
          ToolCalls = toolCalls
        };

        return new ChatCompletionResponse
        {
          Id = _id ?? string.Empty,
          Model = _model ?? string.Empty,
          Usage = _usage,
          Choices =
          [
            new ChatChoice
            {
              FinishReason = _finishReason,
              NativeFinishReason = _nativeFinishReason,
              Message = message
            }
          ]
        };
      }

      private void AddToolCall(ToolCallDelta delta)
      {
        _toolCallsByIndex.TryGetValue(delta.Index, out ToolCallBuilder? builder);
        if(builder is null || builder.IsDifferentCall(delta))
        {
          builder = new ToolCallBuilder();
          _toolCalls.Add(builder);
          _toolCallsByIndex[delta.Index] = builder;
        }

        builder.Add(delta);
      }
    }

    private sealed class ToolCallBuilder
    {
      private readonly StringBuilder _arguments = new();
      private string? _type;
      private string? _name;
      private string? _id;

      // Some providers reuse one index for parallel calls; a new id, or a new name once arguments have started, means a new call.
      public bool IsDifferentCall(ToolCallDelta delta)
      {
        if(!string.IsNullOrEmpty(delta.Id) && !string.IsNullOrEmpty(_id))
        {
          return delta.Id != _id;
        }

        return !string.IsNullOrEmpty(delta.Function?.Name) && !string.IsNullOrEmpty(_name) && _arguments.Length > 0;
      }

      public void Add(ToolCallDelta delta)
      {
        if(string.IsNullOrEmpty(_id))
        {
          _id = delta.Id;
        }

        if(string.IsNullOrEmpty(_name))
        {
          _name = delta.Function?.Name;
        }

        _type ??= delta.Type;
        _arguments.Append(delta.Function?.Arguments);
      }

      public ToolCall Build() => new()
      {
        Id = _id ?? string.Empty,
        Type = _type ?? "function",
        Function = new FunctionCall
        {
          Name = _name ?? string.Empty,
          Arguments = _arguments.ToString()
        }
      };
    }
  }
}
