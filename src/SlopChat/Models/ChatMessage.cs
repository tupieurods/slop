using System.Text.Json.Serialization;

namespace SlopChat.Models
{
  public class ChatMessage
  {
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ToolCall>? ToolCalls { get; set; }

    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; set; }

    [JsonIgnore]
    public string? TextContent => Content as string;

    public static ChatMessage System(string content) => new() { Role = "system", Content = content };
    public static ChatMessage User(string content) => new() { Role = "user", Content = content };
    public static ChatMessage Assistant(string content) => new() { Role = "assistant", Content = content };

    public static ChatMessage UserMultimodal(List<ContentPart> parts) => new()
    {
      Role = "user",
      Content = parts
    };

    public static ChatMessage Assistant(List<ToolCall> toolCalls) => new()
    {
      Role = "assistant",
      ToolCalls = toolCalls
    };

    public static ChatMessage Tool(string toolCallId, string content) => new()
    {
      Role = "tool",
      ToolCallId = toolCallId,
      Content = content
    };
  }
}
