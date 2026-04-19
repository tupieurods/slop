using System.Text.Json.Serialization;

namespace SlopChat.Models
{
  public class ChatCompletionResponse
  {
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<ChatChoice> Choices { get; set; } = [];

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
  }

  public class ChatChoice
  {
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }

    [JsonPropertyName("message")]
    public ChatChoiceMessage? Message { get; set; }
  }

  public class ChatChoiceMessage
  {
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<ToolCall>? ToolCalls { get; set; }
  }
}
