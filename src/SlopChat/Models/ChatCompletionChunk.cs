using System.Text.Json.Serialization;

namespace SlopChat.Models
{
  public class ChatCompletionChunk
  {
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("choices")]
    public List<ChatCompletionChunkChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public CompletionUsage? Usage { get; set; }

    [JsonPropertyName("error")]
    public ChatCompletionStreamError? Error { get; set; }
  }
}
