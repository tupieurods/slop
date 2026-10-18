using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlopChat.Models
{
  public class ChatCompletionChunkDelta
  {
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; }

    [JsonPropertyName("reasoning_details")]
    public List<JsonElement>? ReasoningDetails { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<ToolCallDelta>? ToolCalls { get; set; }
  }
}
