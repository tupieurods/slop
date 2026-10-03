using System.Text.Json.Serialization;

namespace SlopChat.Models
{
  public class ChatCompletionChunkChoice
  {
    [JsonPropertyName("delta")]
    public ChatCompletionChunkDelta? Delta { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }

    [JsonPropertyName("native_finish_reason")]
    public string? NativeFinishReason { get; set; }
  }
}
