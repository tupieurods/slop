using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
      private readonly ReasoningDetailsBuilder _reasoningDetails = new();
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
          foreach(JsonElement detail in delta.ReasoningDetails ?? [])
          {
            _reasoningDetails.Add(detail);
          }

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
          ReasoningDetails = _reasoningDetails.Build(),
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

    private sealed class ReasoningDetailsBuilder
    {
      private readonly List<JsonObject> _details = [];
      private readonly Dictionary<int, JsonObject> _detailsByIndex = [];
      private static readonly HashSet<string> ConcatenatedFields = ["text", "summary"];

      public void Add(JsonElement fragment)
      {
        if(fragment.ValueKind != JsonValueKind.Object)
        {
          return;
        }

        JsonObject incoming = JsonObject.Create(fragment)!;
        int? index = incoming["index"] is JsonValue indexValue && indexValue.TryGetValue(out int i) ? i : null;
        if(index is null)
        {
          _details.Add(incoming);
          return;
        }

        if(!_detailsByIndex.TryGetValue(index.Value, out JsonObject? existing) || IsDifferentEntry(existing, incoming))
        {
          _details.Add(incoming);
          _detailsByIndex[index.Value] = incoming;
          return;
        }

        Merge(existing, incoming);
      }

      public List<JsonElement>? Build() => _details.Count > 0
        ? [.._details.Select(d => JsonSerializer.SerializeToElement(d))]
        : null;

      private static bool IsDifferentEntry(JsonObject existing, JsonObject incoming)
      {
        string? incomingType = GetString(incoming, "type");
        if(incomingType is not null && incomingType != GetString(existing, "type"))
        {
          return true;
        }

        string? incomingId = GetString(incoming, "id");
        string? existingId = GetString(existing, "id");
        return !string.IsNullOrEmpty(incomingId) && !string.IsNullOrEmpty(existingId) && incomingId != existingId;
      }

      private static void Merge(JsonObject existing, JsonObject incoming)
      {
        foreach((string key, JsonNode? value) in incoming)
        {
          if(ConcatenatedFields.Contains(key) && GetString(existing, key) is { } current && GetString(incoming, key) is { } addition)
          {
            existing[key] = current + addition;
            continue;
          }

          if(value is not null || !existing.ContainsKey(key))
          {
            existing[key] = value?.DeepClone();
          }
        }
      }

      private static string? GetString(JsonObject obj, string key)
        => obj[key] is JsonValue value && value.TryGetValue(out string? s) ? s : null;
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
