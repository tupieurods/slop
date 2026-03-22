using System.Text.Json.Serialization;

namespace SlopChat.Models
{
  public class OpenRouterResponse
  {
    [JsonPropertyName("choices")]
    public List<OpenRouterChoice> Choices { get; set; } = new();
  }

  public class OpenRouterChoice
  {
    [JsonPropertyName("message")]
    public ChatMessage Message { get; set; } = new();
  }
}